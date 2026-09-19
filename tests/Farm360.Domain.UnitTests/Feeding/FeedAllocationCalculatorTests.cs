using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Allocation;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Feeding;

/// <summary>
/// Tests for the split that decides every per-animal feed cost in Farm360 (docs/32 GAP-1).
/// </summary>
/// <remarks>
/// The property that matters most is losslessness: whatever the herd looks like, the shares must
/// add back to the group total exactly. A few paisa lost per pen per day compounds into a
/// permanent, unexplainable gap between animal costs and the feed bill.
/// </remarks>
public class FeedAllocationCalculatorTests
{
    private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static List<FeedAllocationSubject> Herd(params (Guid Id, decimal Kg)[] animals) =>
        animals.Select(a => new FeedAllocationSubject(a.Id, a.Kg)).ToList();

    [Fact]
    public void MetabolicWeight_GivesTheHeavierAnimalMoreFeed_ButLessThanItsWeightRatio()
    {
        // 400 kg vs 100 kg: live weight says 4x, metabolic weight says 4^0.75 = 2.83x.
        // That damping is the whole point of the W^0.75 rule.
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 400m), (B, 100m)),
            totalKg: 10m,
            totalCostBdt: 1000m,
            FeedAllocationMethod.MetabolicWeight,
            metabolicExponent: 0.75d,
            fallbackWeightKg: 150m);

        var heavy = shares.Single(s => s.AnimalId == A);
        var light = shares.Single(s => s.AnimalId == B);

        var ratio = heavy.AllocatedKg / light.AllocatedKg;

        ratio.Should().BeGreaterThan(1m, "the heavier animal eats more");
        ratio.Should().BeLessThan(4m, "metabolic scaling damps the weight difference");
        ratio.Should().BeApproximately(2.83m, 0.02m, "4^0.75 = 2.828");
    }

    [Theory]
    [InlineData(FeedAllocationMethod.MetabolicWeight)]
    [InlineData(FeedAllocationMethod.EqualPerHead)]
    [InlineData(FeedAllocationMethod.LiveWeight)]
    public void Allocation_IsLossless_ForAwkwardTotals(FeedAllocationMethod method)
    {
        // 10 / 3 and 1000 / 3 both recur; naive rounding strands a remainder.
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 180m), (B, 240m), (C, 305m)),
            totalKg: 10m,
            totalCostBdt: 1000m,
            method,
            metabolicExponent: 0.75d,
            fallbackWeightKg: 150m);

        shares.Sum(s => s.AllocatedKg).Should().Be(10m);
        shares.Sum(s => s.AllocatedCostBdt).Should().Be(1000m);
    }

    [Fact]
    // CA5394: a fixed-seed PRNG is exactly what a reproducible property test needs; this is not
    // a security context.
#pragma warning disable CA5394
    public void Allocation_IsLossless_AcrossManyRandomHerds()
    {
        var rng = new Random(20260919);

        for (var iteration = 0; iteration < 500; iteration++)
        {
            var headCount = rng.Next(1, 40);
            var subjects = Enumerable.Range(0, headCount)
                .Select(_ => new FeedAllocationSubject(Guid.NewGuid(), Math.Round((decimal)(rng.NextDouble() * 500 + 40), 1)))
                .ToList();

            var totalKg = Math.Round((decimal)(rng.NextDouble() * 200 + 1), 3);
            var totalCost = Math.Round((decimal)(rng.NextDouble() * 20000 + 1), 2);

            var shares = FeedAllocationCalculator.Allocate(
                subjects, totalKg, totalCost,
                FeedAllocationMethod.MetabolicWeight, 0.75d, 150m);

            shares.Sum(s => s.AllocatedKg).Should().Be(totalKg, "kg must reconcile for herd of {0}", headCount);
            shares.Sum(s => s.AllocatedCostBdt).Should().Be(totalCost, "cost must reconcile for herd of {0}", headCount);
        }
    }
#pragma warning restore CA5394

    [Fact]
    public void Allocation_IsDeterministic_RegardlessOfInputOrder()
    {
        // The daily job and a later backfill must not disagree just because the database
        // returned rows in a different order.
        var forward = Herd((A, 180m), (B, 240m), (C, 305m));
        var reversed = Herd((C, 305m), (B, 240m), (A, 180m));

        var first = FeedAllocationCalculator.Allocate(forward, 7m, 777.77m, FeedAllocationMethod.MetabolicWeight, 0.75d, 150m);
        var second = FeedAllocationCalculator.Allocate(reversed, 7m, 777.77m, FeedAllocationMethod.MetabolicWeight, 0.75d, 150m);

        first.Should().BeEquivalentTo(second);
    }

    [Fact]
    public void AnimalWithNoRecordedWeight_UsesTheFallback_SoItCannotTakeAZeroShare()
    {
        // An unweighed animal taking nothing would silently push its feed onto its pen-mates.
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 200m), (B, 0m)),
            totalKg: 10m,
            totalCostBdt: 500m,
            FeedAllocationMethod.MetabolicWeight,
            0.75d,
            fallbackWeightKg: 150m);

        shares.Single(s => s.AnimalId == B).AllocatedKg.Should().BeGreaterThan(0m);
        shares.Sum(s => s.AllocatedKg).Should().Be(10m);
    }

    [Fact]
    public void EveryWeightUnknown_FallsBackToAnEqualSplit_RatherThanDividingByZero()
    {
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 0m), (B, 0m)),
            totalKg: 9m,
            totalCostBdt: 300m,
            FeedAllocationMethod.LiveWeight,
            0.75d,
            fallbackWeightKg: 0m);

        shares.Should().OnlyContain(s => s.AllocatedKg == 4.5m);
        shares.Sum(s => s.AllocatedCostBdt).Should().Be(300m);
    }

    [Fact]
    public void SingleAnimal_TakesTheWholeAmount()
    {
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 220m)), 3.456m, 153.09m,
            FeedAllocationMethod.MetabolicWeight, 0.75d, 150m);

        shares.Should().ContainSingle();
        shares[0].AllocatedKg.Should().Be(3.456m);
        shares[0].AllocatedCostBdt.Should().Be(153.09m);
    }

    [Fact]
    public void EmptyHerd_AllocatesNothing()
    {
        FeedAllocationCalculator.Allocate([], 10m, 500m, FeedAllocationMethod.MetabolicWeight, 0.75d, 150m)
            .Should().BeEmpty();
    }

    [Fact]
    public void ShareFactors_SumToOne()
    {
        var shares = FeedAllocationCalculator.Allocate(
            Herd((A, 180m), (B, 240m), (C, 305m)), 10m, 1000m,
            FeedAllocationMethod.MetabolicWeight, 0.75d, 150m);

        shares.Sum(s => s.ShareFactor).Should().BeApproximately(1m, 0.000_01m);
    }

    [Theory]
    [InlineData(100, 0.75, 31.62)]
    [InlineData(400, 0.75, 89.44)]
    [InlineData(250, 0.75, 62.87)]
    public void MetabolicWeight_MatchesTheStandardFormula(double weight, double exponent, double expected)
    {
        var actual = FeedAllocationCalculator.MetabolicWeight((decimal)weight, exponent);
        ((double)actual).Should().BeApproximately(expected, 0.01);
    }
}
