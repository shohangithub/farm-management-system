using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.EventHandlers;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Finance.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Livestock.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>
/// A sold animal stops being fed from its sale date, however late the sale was recorded: plans end,
/// pending entries are skipped, confirmed ones after the sale reversed (stock back, cost 0), and
/// group entries re-split among the animals actually there.
/// </summary>
public class AnimalExitServiceTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid BatchId = Guid.NewGuid();
    private static readonly DateOnly Exit = new(2026, 11, 13);

    private readonly IAnimalRepository _animals = Substitute.For<IAnimalRepository>();
    private readonly IAnimalFeedingPlanRepository _plans = Substitute.For<IAnimalFeedingPlanRepository>();
    private readonly IDailyFeedingEntryRepository _entries = Substitute.For<IDailyFeedingEntryRepository>();
    private readonly IAnimalFeedAllocationRepository _allocations = Substitute.For<IAnimalFeedAllocationRepository>();
    private readonly IInventoryItemRepository _inventory = Substitute.For<IInventoryItemRepository>();
    private readonly IStockTransactionRepository _stock = Substitute.For<IStockTransactionRepository>();
    private readonly ILedgerPostingService _ledger = Substitute.For<ILedgerPostingService>();
    private readonly IAnimalCostLedgerRepository _costLedgers = Substitute.For<IAnimalCostLedgerRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TestRules _rules = new();

    private readonly List<AnimalFeedAllocation> _rows = [];
    private readonly List<StockTransaction> _movements = [];

    private readonly Animal _sold = NewAnimal("SOLD-1");
    private readonly Animal _b = NewAnimal("B-2");
    private readonly Animal _c = NewAnimal("C-3");
    private readonly InventoryItem _feed = new(Guid.NewGuid(), TenantId, FarmId, "Concentrate", "CON-1", InventoryCategory.Feed, "kg", 0m, 100m, 40m);
    private readonly AnimalFeedingPlan _ownPlan;
    private readonly AnimalFeedingPlan _groupPlan;
    private readonly AnimalExitService _sut;

    private sealed class TestRules : IFarmBusinessRulesProvider
    {
        public FarmBusinessRules Rules { get; } = new() { FeedAllocation = FeedAllocationMethod.EqualPerHead };
        public FarmBusinessRules Current => Rules;
    }

    public AnimalExitServiceTests()
    {
        _sold.Sell(80_000m, Exit, Guid.NewGuid(), "Buyer", null);

        _ownPlan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            Exit.AddDays(-30), null, animalId: _sold.Id);
        _groupPlan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            Exit.AddDays(-30), null, batchId: BatchId);

        foreach (var a in new[] { _sold, _b, _c })
        {
            _animals.GetByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
            _animals.GetByIdAcrossTenantsAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        }

        // The batch as it stood on any day after the sale: the sold animal is gone.
        _animals.GetActiveByScopeAcrossTenantsAsync(TenantId, BatchId, null, null, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Animal> { _b, _c });

        _plans.GetAllPlansForAnimalAsync(TenantId, _sold.Id, Arg.Any<CancellationToken>()).Returns(new List<AnimalFeedingPlan> { _ownPlan });
        _plans.GetByIdAsync(_ownPlan.Id, Arg.Any<CancellationToken>()).Returns(_ownPlan);
        _plans.GetByIdAsync(_groupPlan.Id, Arg.Any<CancellationToken>()).Returns(_groupPlan);

        // An in-memory allocation table that honours soft deletes like the real repository.
        _allocations.GetByEntryIdAcrossTenantsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ci => _rows.Where(r => r.DailyFeedingEntryId == ci.Arg<Guid>() && !r.IsDeleted).ToList());
        _allocations.GetByAnimalAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => _rows.Where(r => r.AnimalId == ci.ArgAt<Guid>(0) && !r.IsDeleted
                                            && r.EntryDate >= ci.ArgAt<DateOnly>(1) && r.EntryDate <= ci.ArgAt<DateOnly>(2)).ToList());
        _allocations.When(x => x.AddRange(Arg.Any<IEnumerable<AnimalFeedAllocation>>()))
            .Do(ci => _rows.AddRange(ci.Arg<IEnumerable<AnimalFeedAllocation>>()));

        _inventory.GetByIdAsync(_feed.Id, Arg.Any<CancellationToken>()).Returns(_feed);
        _stock.GetByFarmIdAsync(FarmId, Arg.Any<DateOnly?>(), Arg.Any<DateOnly?>(), Arg.Any<CancellationToken>())
            .Returns(_ => _movements.ToList());
        _stock.When(x => x.AddAsync(Arg.Any<StockTransaction>(), Arg.Any<CancellationToken>()))
            .Do(ci => _movements.Add(ci.Arg<StockTransaction>()));

        _sut = new AnimalExitService(_animals, _plans, _entries, _allocations, new FeedAllocationService(_animals, _rules),
            _inventory, _stock, _ledger, _costLedgers, _rules, Substitute.For<ICurrentUserService>(), _unitOfWork,
            NullLogger<AnimalExitService>.Instance);
    }

    private static Animal NewAnimal(string tag)
    {
        var animal = Animal.Create(TenantId, FarmId, AnimalTag.Create(tag, TagType.EarTag), AnimalSpecies.CattleBeef,
            Guid.NewGuid(), AnimalSex.Male, new DateOnly(2025, 1, 1), AcquisitionType.Purchased, new DateOnly(2026, 1, 1), 50_000m, null);
        animal.RecordWeight(Weight.Create(200m), new DateOnly(2026, 6, 1), Guid.NewGuid(), null);
        return animal;
    }

    private DailyFeedingEntry OwnEntry(DateOnly date, bool confirm)
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, _ownPlan.Id, FarmId, date, Guid.NewGuid(), 5m);
        entry.SetConsumptionCost(40m);
        if (confirm) entry.Confirm(5m);
        _rows.Add(AnimalFeedAllocation.Create(TenantId, _sold.Id, FarmId, entry.Id, _ownPlan.Id, entry.FormulaId, null, date,
            5m, 200m, 40m, FeedAllocationOrigin.IndividualPlan, FeedAllocationMethod.EqualPerHead, 200m, 1m, 1));
        _entries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        return entry;
    }

    /// <summary>A batch entry of 3 kg a head shared by the sold animal, B and C (9 kg, 360 BDT).</summary>
    private DailyFeedingEntry GroupEntry(DateOnly date, bool confirm)
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, _groupPlan.Id, FarmId, date, Guid.NewGuid(), 3m, batchId: BatchId);
        entry.SetConsumptionCost(40m, 3m);
        if (confirm) entry.Confirm(3m);
        foreach (var a in new[] { _sold, _b, _c })
        {
            _rows.Add(AnimalFeedAllocation.Create(TenantId, a.Id, FarmId, entry.Id, _groupPlan.Id, entry.FormulaId, null, date,
                3m, 120m, 40m, FeedAllocationOrigin.GroupPlanShare, FeedAllocationMethod.EqualPerHead, 200m, 1m / 3m, 3, batchId: BatchId));
        }
        _entries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        return entry;
    }

    private void OwnEntriesAre(params DailyFeedingEntry[] entries) =>
        _entries.GetEntriesByAnimalIdAsync(TenantId, _sold.Id, Arg.Any<CancellationToken>()).Returns(entries.ToList());

    private void IssuedFromStock(DailyFeedingEntry entry, decimal kg)
    {
        _feed.DeductStock(kg, Guid.NewGuid());
        _movements.Add(new StockTransaction(Guid.NewGuid(), TenantId, FarmId, _feed.Id, StockTransactionType.PlannedFeedConsumption,
            kg, 40m, _feed.CurrentStock, entry.EntryDate, referenceId: entry.Id));
    }

    private Task<AnimalExitResult> RunAsync() =>
        _sut.ProcessExitAsync(_sold.Id, Exit, AnimalExitService.SoldReason(Exit));

    [Fact]
    public async Task PublishedSale_CancelsTheSoldAnimalsPlans()
    {
        OwnEntriesAre();
        var handler = new AnimalSoldEventHandler(_sut);

        await handler.Handle(new AnimalSoldEvent(Guid.NewGuid(), DateTime.UtcNow, _sold.Id, TenantId, FarmId, Guid.NewGuid(),
            80_000m, Exit, "Buyer", null), CancellationToken.None);

        _ownPlan.Status.Should().Be(FeedingPlanStatus.Cancelled);
        _ownPlan.EndDate.Should().Be(Exit);
        _plans.Received(1).Update(_ownPlan);
    }

    [Fact]
    public async Task PendingEntriesFromTheSaleDay_AreSkippedWithTheReason_EarlierOnesUntouched()
    {
        var before = OwnEntry(Exit.AddDays(-1), confirm: false);
        var onExit = OwnEntry(Exit, confirm: false);
        var after = OwnEntry(Exit.AddDays(2), confirm: false);
        OwnEntriesAre(before, onExit, after);

        var result = await RunAsync();

        result.EntriesSkipped.Should().Be(2);
        before.Status.Should().Be(DailyFeedingEntryStatus.Pending);
        onExit.Status.Should().Be(DailyFeedingEntryStatus.Skipped);
        after.Status.Should().Be(DailyFeedingEntryStatus.Skipped);
        after.AdjustmentReason.Should().Be("Animal sold on 13 Nov 2026");
        _rows.Where(r => r.DailyFeedingEntryId == after.Id).Should().OnlyContain(r => r.AllocatedKg == 0m && r.AllocatedCostBdt == 0m);
    }

    [Fact]
    public async Task ConfirmedEntryAfterTheSale_IsReversed_StockBack_CostZero_LedgerPostingRemoved()
    {
        var onExit = OwnEntry(Exit, confirm: true);          // it ate that morning: stays
        var after = OwnEntry(Exit.AddDays(1), confirm: true); // it was gone: reversed
        IssuedFromStock(after, 5m);
        OwnEntriesAre(onExit, after);

        var result = await RunAsync();

        result.EntriesReversed.Should().Be(1);
        onExit.Status.Should().Be(DailyFeedingEntryStatus.Confirmed);
        after.Status.Should().Be(DailyFeedingEntryStatus.Skipped);
        after.ActualKg.Should().Be(0m);
        after.TotalCostBdt.Should().Be(0m);
        _feed.CurrentStock.Should().Be(100m, "the 5 kg deducted for the voided day is back");
        _feed.WeightedAverageCostBdt.Should().Be(40m);
        _movements.Should().ContainSingle(t => t.TransactionType == StockTransactionType.FeedConsumptionReversal
                                               && t.ReferenceId == after.Id && t.Quantity == 5m);
        await _ledger.Received(1).ReverseAsync(FarmId, after.Id, TransactionCategory.FeedCost, Arg.Any<CancellationToken>());
        await _ledger.DidNotReceive().ReverseAsync(FarmId, onExit.Id, Arg.Any<TransactionCategory>(), Arg.Any<CancellationToken>());
        _rows.Single(r => r.DailyFeedingEntryId == after.Id).AllocatedCostBdt.Should().Be(0m);
    }

    [Fact]
    public async Task GroupEntries_AreResplitWithoutTheSoldAnimal()
    {
        OwnEntriesAre();
        var pending = GroupEntry(Exit, confirm: false);
        var confirmed = GroupEntry(Exit.AddDays(1), confirm: true);
        var confirmedOnExitDay = GroupEntry(Exit, confirm: true);

        var result = await RunAsync();

        result.GroupEntriesResplit.Should().Be(2);

        // Pending: a plan — two heads now, so 6 kg and 240 BDT planned.
        var pendingRows = _rows.Where(r => r.DailyFeedingEntryId == pending.Id && !r.IsDeleted).ToList();
        pendingRows.Select(r => r.AnimalId).Should().BeEquivalentTo([_b.Id, _c.Id]);
        pendingRows.Should().OnlyContain(r => r.HeadCountAtAllocation == 2 && r.AllocatedKg == 3m);
        pending.TotalCostBdt.Should().Be(240m);

        // Confirmed after the sale: the 9 kg / 360 BDT given to the pen is re-split between B and C.
        var confirmedRows = _rows.Where(r => r.DailyFeedingEntryId == confirmed.Id && !r.IsDeleted).ToList();
        confirmedRows.Select(r => r.AnimalId).Should().BeEquivalentTo([_b.Id, _c.Id]);
        confirmedRows.Should().OnlyContain(r => r.HeadCountAtAllocation == 2 && r.AllocatedKg == 4.5m && r.AllocatedCostBdt == 180m);
        confirmed.Status.Should().Be(DailyFeedingEntryStatus.Confirmed, "the feed really was given; stock is not touched");
        _movements.Should().BeEmpty();

        // Confirmed on the sale day itself: it ate that morning, so its share stays.
        _rows.Where(r => r.DailyFeedingEntryId == confirmedOnExitDay.Id && !r.IsDeleted)
            .Should().HaveCount(3).And.Contain(r => r.AnimalId == _sold.Id);
    }

    [Fact]
    public async Task SecondRun_ChangesNothing()
    {
        var pendingOwn = OwnEntry(Exit.AddDays(1), confirm: false);
        var confirmedOwn = OwnEntry(Exit.AddDays(2), confirm: true);
        IssuedFromStock(confirmedOwn, 5m);
        OwnEntriesAre(pendingOwn, confirmedOwn);
        GroupEntry(Exit.AddDays(1), confirm: true);

        var first = await RunAsync();
        var rowsAfterFirst = _rows.Select(r => (r.Id, r.AllocatedKg, r.AllocatedCostBdt, r.IsDeleted)).ToList();
        var stockAfterFirst = _feed.CurrentStock;
        var movementsAfterFirst = _movements.Count;

        var second = await RunAsync();

        first.ChangedAnything.Should().BeTrue();
        second.Should().Be(AnimalExitResult.None);
        _rows.Select(r => (r.Id, r.AllocatedKg, r.AllocatedCostBdt, r.IsDeleted)).Should().Equal(rowsAfterFirst);
        _feed.CurrentStock.Should().Be(stockAfterFirst);
        _movements.Should().HaveCount(movementsAfterFirst);
        await _ledger.Received(1).ReverseAsync(FarmId, confirmedOwn.Id, TransactionCategory.FeedCost, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IndividualAllocation_ForADayAfterTheSale_IsEmpty()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, _ownPlan.Id, FarmId, Exit.AddDays(1), Guid.NewGuid(), 5m);

        var rows = await new FeedAllocationService(_animals, _rules).BuildAllocationsAsync(entry, _ownPlan);

        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ConfirmingAnEntryForADayOnOrAfterTheSale_IsRefusedWithTheReason()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, _ownPlan.Id, FarmId, Exit, Guid.NewGuid(), 5m);
        _entries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        var handler = new Farm360.Application.Feeding.Commands.DailyFeedingEntries.ConfirmDailyFeedingEntryCommandHandler(
            _entries, Substitute.For<IFeedFormulaRepository>(), Substitute.For<IFeedIngredientRepository>(), _inventory,
            _unitOfWork, Substitute.For<MediatR.IPublisher>(), _plans, _allocations, _rules, null, _animals);

        var act = () => handler.Handle(new Farm360.Application.Feeding.Commands.DailyFeedingEntries.ConfirmDailyFeedingEntryCommand(entry.Id, 5m), CancellationToken.None);

        (await act.Should().ThrowAsync<Farm360.Domain.Exceptions.DomainException>())
            .WithMessage("SOLD-1 was sold on 13 Nov 2026*");
        entry.Status.Should().Be(DailyFeedingEntryStatus.Pending);
    }
}
