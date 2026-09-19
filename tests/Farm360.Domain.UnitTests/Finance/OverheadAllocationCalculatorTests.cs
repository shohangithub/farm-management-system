using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance.Allocation;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Finance;

/// <summary>
/// Tests for the split that decides every animal's share of farm labour and overhead
/// (docs/32 GAP-2). As with feed, losslessness is the property that matters most: the shares
/// must add back to the expense exactly, or the ledger drifts from the P&amp;L.
/// </summary>
public class OverheadAllocationCalculatorTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static List<OverheadAllocationSubject> Herd(params (Guid Id, decimal Days, decimal Kg)[] animals) =>
        animals.Select(a => new OverheadAllocationSubject(a.Id, a.Days, a.Kg)).ToList();

    // ── Head-days ───────────────────────────────────────────────────────────

    [Fact]
    public void HeadDays_CountsTheWholePeriod_WhenTheAnimalWasThereThroughout()
    {
        var days = OverheadAllocationCalculator.HeadDays(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            arrived: new DateOnly(2025, 1, 1), left: null);

        days.Should().Be(30m);
    }

    [Fact]
    public void HeadDays_IsProRated_ForAnAnimalSoldMidPeriod()
    {
        // Sold on the 10th: ten days of the month's labour, not all of it.
        var days = OverheadAllocationCalculator.HeadDays(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            arrived: new DateOnly(2025, 1, 1), left: new DateOnly(2026, 4, 10));

        days.Should().Be(10m);
    }

    [Fact]
    public void HeadDays_IsProRated_ForAnAnimalBoughtMidPeriod()
    {
        var days = OverheadAllocationCalculator.HeadDays(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            arrived: new DateOnly(2026, 4, 21), left: null);

        days.Should().Be(10m, "21st to 30th inclusive");
    }

    [Fact]
    public void HeadDays_IsZero_WhenTheStayDoesNotOverlapThePeriod()
    {
        OverheadAllocationCalculator.HeadDays(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            arrived: new DateOnly(2026, 5, 1), left: null)
            .Should().Be(0m);

        OverheadAllocationCalculator.HeadDays(
            new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30),
            arrived: new DateOnly(2025, 1, 1), left: new DateOnly(2026, 3, 15))
            .Should().Be(0m);
    }

    // ── Allocation ──────────────────────────────────────────────────────────

    [Fact]
    public void PerHeadDay_SharesInProportionToDaysPresent()
    {
        // 30 days, 15 days, 15 days out of 60 head-days: 50%, 25%, 25% of 12,000.
        var shares = OverheadAllocationCalculator.Allocate(
            Herd((A, 30m, 200m), (B, 15m, 200m), (C, 15m, 200m)),
            totalAmountBdt: 12_000m,
            OverheadAllocationMethod.PerHeadDay,
            fallbackWeightKg: 150m);

        shares.Single(s => s.AnimalId == A).AllocatedAmountBdt.Should().Be(6_000m);
        shares.Single(s => s.AnimalId == B).AllocatedAmountBdt.Should().Be(3_000m);
        shares.Single(s => s.AnimalId == C).AllocatedAmountBdt.Should().Be(3_000m);
    }

    [Fact]
    public void PerLiveWeight_WeightsByWeightDays_NotWeightAlone()
    {
        // A: 400 kg for 10 days = 4,000 weight-days. B: 100 kg for 30 days = 3,000.
        // Weight alone would give A four times B's share; weight-days gives it 4:3.
        var shares = OverheadAllocationCalculator.Allocate(
            Herd((A, 10m, 400m), (B, 30m, 100m)),
            totalAmountBdt: 7_000m,
            OverheadAllocationMethod.PerLiveWeight,
            fallbackWeightKg: 150m);

        shares.Single(s => s.AnimalId == A).AllocatedAmountBdt.Should().Be(4_000m);
        shares.Single(s => s.AnimalId == B).AllocatedAmountBdt.Should().Be(3_000m);
    }

    [Theory]
    [InlineData(OverheadAllocationMethod.PerHeadDay)]
    [InlineData(OverheadAllocationMethod.PerLiveWeight)]
    public void Allocation_IsLossless_ForAwkwardAmounts(OverheadAllocationMethod method)
    {
        var shares = OverheadAllocationCalculator.Allocate(
            Herd((A, 30m, 180m), (B, 30m, 240m), (C, 30m, 305m)),
            totalAmountBdt: 10_000m,
            method,
            fallbackWeightKg: 150m);

        shares.Sum(s => s.AllocatedAmountBdt).Should().Be(10_000m);
    }

    [Fact]
#pragma warning disable CA5394 // Fixed-seed PRNG for a reproducible property test, not security.
    public void Allocation_IsLossless_AcrossManyRandomHerdsAndAmounts()
    {
        var rng = new Random(20260920);

        for (var i = 0; i < 500; i++)
        {
            var headCount = rng.Next(1, 60);
            var subjects = Enumerable.Range(0, headCount)
                .Select(_ => new OverheadAllocationSubject(
                    Guid.NewGuid(),
                    rng.Next(1, 31),
                    Math.Round((decimal)(rng.NextDouble() * 500 + 40), 1)))
                .ToList();

            var amount = Math.Round((decimal)(rng.NextDouble() * 250_000 + 1), 2);

            var shares = OverheadAllocationCalculator.Allocate(
                subjects, amount, OverheadAllocationMethod.PerHeadDay, 150m);

            shares.Sum(s => s.AllocatedAmountBdt)
                .Should().Be(amount, "overhead must reconcile for a herd of {0}", headCount);
        }
    }
#pragma warning restore CA5394

    [Fact]
    public void AnimalPresentForNoDays_ReceivesNothing()
    {
        var shares = OverheadAllocationCalculator.Allocate(
            Herd((A, 30m, 200m), (B, 0m, 200m)),
            totalAmountBdt: 5_000m,
            OverheadAllocationMethod.PerHeadDay,
            150m);

        shares.Single(s => s.AnimalId == B).AllocatedAmountBdt.Should().Be(0m);
        shares.Single(s => s.AnimalId == A).AllocatedAmountBdt.Should().Be(5_000m);
    }

    [Fact]
    public void Allocation_IsDeterministic_RegardlessOfInputOrder()
    {
        var forward = Herd((A, 11m, 180m), (B, 13m, 240m), (C, 7m, 305m));
        var reversed = Herd((C, 7m, 305m), (B, 13m, 240m), (A, 11m, 180m));

        var first = OverheadAllocationCalculator.Allocate(forward, 9_999.99m, OverheadAllocationMethod.PerHeadDay, 150m);
        var second = OverheadAllocationCalculator.Allocate(reversed, 9_999.99m, OverheadAllocationMethod.PerHeadDay, 150m);

        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void EmptyHerd_AllocatesNothing()
    {
        OverheadAllocationCalculator.Allocate([], 5_000m, OverheadAllocationMethod.PerHeadDay, 150m)
            .Should().BeEmpty();
    }
}
