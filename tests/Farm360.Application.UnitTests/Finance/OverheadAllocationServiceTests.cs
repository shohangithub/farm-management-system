using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>
/// Cover for GAP-2 (docs/32): farm labour and overhead must reach the animal cost ledger, and
/// must do so exactly once however many times the allocation is run.
/// </summary>
public class OverheadAllocationServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly DateOnly From = new(2026, 4, 1);
    private static readonly DateOnly To = new(2026, 4, 30);

    private readonly IFinancialTransactionRepository _transactions = Substitute.For<IFinancialTransactionRepository>();
    private readonly IAnimalCostLedgerRepository _ledgers = Substitute.For<IAnimalCostLedgerRepository>();
    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TestRules _rules = new();

    /// <summary>
    /// In-memory stand-in for the allocation table, including its unique (transaction, animal)
    /// constraint — so duplicate prevention is exercised, not assumed.
    /// </summary>
    private readonly FakeAllocationRepository _allocations = new();

    private readonly OverheadAllocationService _sut;

    public OverheadAllocationServiceTests()
    {
        _sut = new OverheadAllocationService(
            _transactions, _allocations, _ledgers, _animals, _rules, _unitOfWork,
            NullLogger<OverheadAllocationService>.Instance);
    }

    private sealed class TestRules : IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Rules { get; } = new();
        public FarmBusinessRules Current => Rules;
    }

    private sealed class FakeAllocationRepository : IAnimalOverheadAllocationRepository
    {
        public List<AnimalOverheadAllocation> Rows { get; } = [];

        public void AddRange(IEnumerable<AnimalOverheadAllocation> allocations)
        {
            foreach (var row in allocations)
            {
                // Mirrors UX_AnimalOverheadAllocations_Transaction_Animal.
                if (Rows.Any(r => r.SourceTransactionId == row.SourceTransactionId && r.AnimalId == row.AnimalId))
                {
                    throw new InvalidOperationException(
                        $"Duplicate allocation for transaction {row.SourceTransactionId} and animal {row.AnimalId}.");
                }

                Rows.Add(row);
            }
        }

        public Task<HashSet<Guid>> GetAllocatedTransactionIdsAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult(Rows
                .Where(r => r.FarmId == farmId && r.PeriodEnd >= from && r.PeriodStart <= to)
                .Select(r => r.SourceTransactionId)
                .ToHashSet());

        public Task<AnimalOverheadTotals> GetTotalsForAnimalAsync(Guid animalId, CancellationToken ct = default) =>
            Task.FromResult(Totals(animalId));

        public Task<IReadOnlyList<AnimalOverheadTotals>> GetTotalsForAnimalsAsync(IEnumerable<Guid> animalIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadTotals>>(animalIds.Distinct().Select(Totals).ToList());

        public Task<IReadOnlyList<AnimalOverheadAllocation>> GetByAnimalAsync(Guid animalId, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AnimalOverheadAllocation>>(
                Rows.Where(r => r.AnimalId == animalId).ToList());

        private AnimalOverheadTotals Totals(Guid animalId) => new(
            animalId,
            Rows.Where(r => r.AnimalId == animalId && r.Bucket == OverheadBucket.Labour).Sum(r => r.AllocatedAmountBdt),
            Rows.Where(r => r.AnimalId == animalId && r.Bucket == OverheadBucket.Overhead).Sum(r => r.AllocatedAmountBdt));
    }

    // ── Fixtures ────────────────────────────────────────────────────────────

    private static Animal AnimalPresent(string tag, DateOnly acquired, DateOnly? sold = null, decimal weightKg = 200m)
    {
        var animal = Animal.Create(
            tenantId: TenantId, farmId: FarmId,
            tag: AnimalTag.Create(tag, TagType.EarTag),
            species: AnimalSpecies.CattleBeef, breedId: Guid.NewGuid(), sex: AnimalSex.Male,
            dateOfBirth: new DateOnly(2024, 1, 1),
            acquisitionType: AcquisitionType.Purchased, acquisitionDate: acquired,
            acquisitionPriceBdt: 50_000m, notes: null);

        animal.RecordWeight(Weight.Create(weightKg), acquired, Guid.NewGuid(), null);

        if (sold is not null)
        {
            animal.Sell(80_000m, sold.Value, Guid.NewGuid(), "Buyer", weightKg);
        }

        return animal;
    }

    private static FinancialTransaction Expense(TransactionCategory category, decimal amount, DateOnly date) =>
        FinancialTransaction.Create(
            tenantId: TenantId, farmId: FarmId,
            type: TransactionType.Expense, category: category,
            amountBdt: amount, transactionDate: date.ToDateTime(TimeOnly.MinValue),
            referenceId: $"REF-{category}-{date:yyyyMMdd}", description: category.ToString());

    private void Herd(params Animal[] animals) =>
        _animals.GetPresentDuringPeriodAsync(FarmId, From, To, Arg.Any<CancellationToken>())
            .Returns(animals.ToList());

    private void Expenses(params FinancialTransaction[] transactions) =>
        _transactions.GetUnattributedIndirectCostsAsync(FarmId, From, To, Arg.Any<CancellationToken>())
            .Returns(transactions.ToList());

    private AnimalCostLedger LedgerFor(Animal animal, decimal acquisition = 50_000m)
    {
        var ledger = AnimalCostLedger.Create(TenantId, animal.Id, FarmId, acquisition);
        _ledgers.GetByAnimalIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns(ledger);
        return ledger;
    }

    // ── Posting ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task LabourIsPostedToTheLabourBucket_AndOverheadToTheOverheadBucket()
    {
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal);
        Herd(animal);
        Expenses(
            Expense(TransactionCategory.LaborCost, 9_000m, new DateOnly(2026, 4, 30)),
            Expense(TransactionCategory.Utilities, 1_500m, new DateOnly(2026, 4, 30)));

        var result = await _sut.AllocateAsync(FarmId, From, To);

        result.TransactionsAllocated.Should().Be(2);
        ledger.TotalLaborCostBdt.Should().Be(9_000m);
        ledger.TotalOverheadBdt.Should().Be(1_500m);
    }

    [Fact]
    public async Task TotalCostBecomesFullAbsorption_OnceOverheadIsPosted()
    {
        // The GAP-2 headline: before this ran, TotalCost was acquisition + feed + vet only.
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal, acquisition: 80_000m);
        ledger.UpdateFeedCost(19_324.99m);
        ledger.RecordCost(TransactionCategory.VeterinaryCost, 2_500m);

        var directOnly = ledger.TotalCostBdt;

        Herd(animal);
        Expenses(
            Expense(TransactionCategory.LaborCost, 6_000m, new DateOnly(2026, 4, 15)),
            Expense(TransactionCategory.Transport, 900m, new DateOnly(2026, 4, 20)));

        await _sut.AllocateAsync(FarmId, From, To);

        ledger.TotalCostBdt.Should().Be(directOnly + 6_900m);
        ledger.TotalCostBdt.Should().Be(80_000m + 19_324.99m + 2_500m + 6_000m + 900m);
    }

    [Fact]
    public async Task ExpenseIsSharedByHeadDays_SoAMidMonthSaleCarriesOnlyItsDays()
    {
        // Sold on the 10th: 10 of 40 head-days, so a quarter of the wage bill.
        var stayed = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var sold = AnimalPresent("B-002", new DateOnly(2025, 1, 1), sold: new DateOnly(2026, 4, 10));
        var stayedLedger = LedgerFor(stayed);
        var soldLedger = LedgerFor(sold);

        Herd(stayed, sold);
        Expenses(Expense(TransactionCategory.LaborCost, 8_000m, new DateOnly(2026, 4, 30)));

        await _sut.AllocateAsync(FarmId, From, To);

        stayedLedger.TotalLaborCostBdt.Should().Be(6_000m, "30 of 40 head-days");
        soldLedger.TotalLaborCostBdt.Should().Be(2_000m, "10 of 40 head-days");
        (stayedLedger.TotalLaborCostBdt + soldLedger.TotalLaborCostBdt).Should().Be(8_000m);
    }

    [Fact]
    public async Task DirectCostCategoriesAreNeverAllocated()
    {
        // Feed and veterinary already reach the ledger through their own modules; allocating
        // them here would double-count them.
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal);
        Herd(animal);
        Expenses(
            Expense(TransactionCategory.FeedCost, 5_000m, new DateOnly(2026, 4, 10)),
            Expense(TransactionCategory.VeterinaryCost, 3_000m, new DateOnly(2026, 4, 12)));

        var result = await _sut.AllocateAsync(FarmId, From, To);

        result.TransactionsAllocated.Should().Be(0);
        ledger.TotalLaborCostBdt.Should().Be(0m);
        ledger.TotalOverheadBdt.Should().Be(0m);
    }

    // ── Idempotency and duplicate prevention ────────────────────────────────

    [Fact]
    public async Task RunningTwiceOverTheSamePeriod_ChangesNothing()
    {
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal);
        Herd(animal);
        Expenses(Expense(TransactionCategory.LaborCost, 12_000m, new DateOnly(2026, 4, 30)));

        var first = await _sut.AllocateAsync(FarmId, From, To);
        var labourAfterFirst = ledger.TotalLaborCostBdt;
        var totalAfterFirst = ledger.TotalCostBdt;

        var second = await _sut.AllocateAsync(FarmId, From, To);

        first.TransactionsAllocated.Should().Be(1);
        second.TransactionsAllocated.Should().Be(0);
        second.TransactionsSkippedAlreadyAllocated.Should().Be(1);

        labourAfterFirst.Should().Be(12_000m);
        ledger.TotalLaborCostBdt.Should().Be(12_000m, "a re-run must not double the labour cost");
        ledger.TotalCostBdt.Should().Be(totalAfterFirst);
        _allocations.Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunningThreeTimes_StillPostsTheExpenseExactlyOnce()
    {
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal);
        Herd(animal);
        Expenses(Expense(TransactionCategory.Utilities, 3_333.33m, new DateOnly(2026, 4, 5)));

        await _sut.AllocateAsync(FarmId, From, To);
        await _sut.AllocateAsync(FarmId, From, To);
        await _sut.AllocateAsync(FarmId, From, To);

        _allocations.Rows.Should().HaveCount(1);
        ledger.TotalOverheadBdt.Should().Be(3_333.33m);
    }

    [Fact]
    public async Task TheUniqueConstraintIsNeverReached_BecauseTheServiceSkipsFirst()
    {
        // The fake repository throws on a duplicate, exactly as the database index would.
        // This asserts the service's own guard holds, so the constraint stays a backstop
        // rather than the mechanism.
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        LedgerFor(animal);
        Herd(animal);
        Expenses(Expense(TransactionCategory.LaborCost, 1_000m, new DateOnly(2026, 4, 2)));

        await _sut.AllocateAsync(FarmId, From, To);

        var rerun = async () => await _sut.AllocateAsync(FarmId, From, To);
        await rerun.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ANewExpenseInAnAlreadyRunPeriod_IsPickedUpWithoutDisturbingTheRest()
    {
        // A late invoice posted after the month was first allocated.
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        var ledger = LedgerFor(animal);
        Herd(animal);

        var wages = Expense(TransactionCategory.LaborCost, 10_000m, new DateOnly(2026, 4, 28));
        Expenses(wages);
        await _sut.AllocateAsync(FarmId, From, To);

        var lateBill = Expense(TransactionCategory.Utilities, 2_000m, new DateOnly(2026, 4, 29));
        Expenses(wages, lateBill);
        var second = await _sut.AllocateAsync(FarmId, From, To);

        second.TransactionsAllocated.Should().Be(1);
        second.TransactionsSkippedAlreadyAllocated.Should().Be(1);
        ledger.TotalLaborCostBdt.Should().Be(10_000m, "unchanged");
        ledger.TotalOverheadBdt.Should().Be(2_000m, "newly added");
    }

    // ── Edge cases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExpenseWithNoAnimalsOnFarm_IsLeftUnallocated()
    {
        Herd();
        Expenses(Expense(TransactionCategory.LaborCost, 5_000m, new DateOnly(2026, 4, 10)));

        var result = await _sut.AllocateAsync(FarmId, From, To);

        result.TransactionsAllocated.Should().Be(0);
        result.TransactionsSkippedNoAnimals.Should().Be(1);
        _allocations.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task AnimalWithNoLedgerYet_IsSkippedRatherThanGivenAnEmptyOne()
    {
        // Creating a ledger here would set the acquisition cost to zero and corrupt the margin.
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        _ledgers.GetByAnimalIdAsync(animal.Id, Arg.Any<CancellationToken>()).Returns((AnimalCostLedger?)null);
        Herd(animal);
        Expenses(Expense(TransactionCategory.LaborCost, 4_000m, new DateOnly(2026, 4, 10)));

        var result = await _sut.AllocateAsync(FarmId, From, To);

        result.AllocationsWritten.Should().Be(1, "the allocation is still recorded for audit");
        result.LedgersUpdated.Should().Be(0);
    }

    [Fact]
    public async Task EachAllocationRecordsHowItWasComputed()
    {
        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        LedgerFor(animal);
        Herd(animal);
        Expenses(Expense(TransactionCategory.LaborCost, 6_000m, new DateOnly(2026, 4, 18)));

        await _sut.AllocateAsync(FarmId, From, To, isBackfill: true);

        var row = _allocations.Rows.Single();
        row.Method.Should().Be(OverheadAllocationMethod.PerHeadDay);
        row.Bucket.Should().Be(OverheadBucket.Labour);
        row.HeadDays.Should().Be(30m);
        row.HeadCountAtAllocation.Should().Be(1);
        row.ShareFactor.Should().Be(1m);
        row.PeriodStart.Should().Be(new DateOnly(2026, 4, 1), "a monthly wage bill covers the month");
        row.PeriodEnd.Should().Be(new DateOnly(2026, 4, 30));
        row.IsBackfilled.Should().BeTrue();
    }

    [Fact]
    public async Task TransactionDatePeriodRule_NarrowsTheCostToASingleDay()
    {
        // Proves the period basis is configuration, not a hard-coded convention.
        _rules.Rules.OverheadCostPeriod = OverheadCostPeriod.TransactionDate;

        var animal = AnimalPresent("B-001", new DateOnly(2025, 1, 1));
        LedgerFor(animal);
        Herd(animal);
        Expenses(Expense(TransactionCategory.Transport, 1_200m, new DateOnly(2026, 4, 18)));

        await _sut.AllocateAsync(FarmId, From, To);

        var row = _allocations.Rows.Single();
        row.PeriodStart.Should().Be(new DateOnly(2026, 4, 18));
        row.PeriodEnd.Should().Be(new DateOnly(2026, 4, 18));
        row.HeadDays.Should().Be(1m);
    }

    [Fact]
    public async Task AllocationsAcrossAHerdReconcileToTheExpense()
    {
        var herd = Enumerable.Range(1, 7)
            .Select(i => AnimalPresent($"B-{i:000}", new DateOnly(2026, 4, i)))
            .ToArray();

        var ledgers = herd.Select(a => LedgerFor(a)).ToList();
        Herd(herd);
        Expenses(Expense(TransactionCategory.LaborCost, 10_000m, new DateOnly(2026, 4, 30)));

        await _sut.AllocateAsync(FarmId, From, To);

        _allocations.Rows.Sum(r => r.AllocatedAmountBdt).Should().Be(10_000m);
        ledgers.Sum(l => l.TotalLaborCostBdt).Should().Be(10_000m);
    }

    [Fact]
    public async Task InvertedPeriod_IsRejected()
    {
        var act = async () => await _sut.AllocateAsync(FarmId, To, From);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
