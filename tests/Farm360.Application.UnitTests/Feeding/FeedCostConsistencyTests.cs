using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Commands.FeedFormulas;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Application.Finance.Services;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Feeding;

/// <summary>Feed quantity / unit / cost consistency: gram pricing, % normalisation, group basis, ledger idempotency, skip.</summary>
public class FeedCostConsistencyTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();

    private readonly IFeedIngredientRepository _ingredients = Substitute.For<IFeedIngredientRepository>();
    private readonly IInventoryItemRepository _inventory = Substitute.For<IInventoryItemRepository>();
    private readonly IUnitService _units = Substitute.For<IUnitService>();

    private InventoryItem Item(string name, string unit, decimal stock, decimal cost)
    {
        var item = new InventoryItem(Guid.NewGuid(), TenantId, FarmId, name, name[..3].ToUpperInvariant(), InventoryCategory.Feed, unit, 0m,
            initialStock: stock, initialCostBdt: cost);
        _inventory.GetByIdAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        // g → kg × 1000; kg and ml feed as they are.
        _units.KgToItemBaseAsync(item, Arg.Any<decimal>(), Arg.Any<CancellationToken>())
            .Returns(ci => unit == "g" ? ci.ArgAt<decimal>(1) * 1000m : ci.ArgAt<decimal>(1));
        return item;
    }

    private FeedIngredient Linked(InventoryItem item, decimal fallbackCost = 0m)
    {
        var ing = new FeedIngredient(Guid.NewGuid(), TenantId, item.Name, FeedCategory.Concentrate, NutritionalProfile.Empty,
            fallbackCost, item.UnitOfMeasure, inventoryItemId: item.Id);
        _ingredients.GetByIdAsync(ing.Id, Arg.Any<CancellationToken>()).Returns(ing);
        return ing;
    }

    private FeedIngredient Unlinked(string name, decimal costPerKg)
    {
        var ing = new FeedIngredient(Guid.NewGuid(), TenantId, name, FeedCategory.Forage, NutritionalProfile.Empty, costPerKg);
        _ingredients.GetByIdAsync(ing.Id, Arg.Any<CancellationToken>()).Returns(ing);
        return ing;
    }

    private FeedCostCalculator Calculator() => new(_ingredients, _inventory, _units);

    // ── gram-unit pricing ──────────────────────────────────────────────────

    [Fact]
    public async Task GramItem_IsPricedPerKgOfFeed_AndDeductedInGrams()
    {
        var premix = Item("Premix", "g", stock: 5_000m, cost: 2m); // 2 BDT per gram
        var formula = new FeedFormula(Guid.NewGuid(), TenantId, "Premix (direct item)", TargetAnimalType.Cattle);
        formula.AddIngredient(Linked(premix).Id, 100m, 2m, NutritionalProfile.Empty);

        var calc = Calculator();
        (await calc.StockItemCostPerKgAsync(premix)).Should().Be(2000m);
        (await calc.FormulaCostPerKgAsync(formula)).Should().Be(2000m);

        var need = await calc.RequirementAsync(formula, 0.05m);
        need.Ingredients.Should().ContainSingle().Which.StockQuantity.Should().Be(50m); // 50 g
        need.TotalCostBdt.Should().Be(100m);
        need.Shortfalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ShortfallCheck_UsesBaseUnitQuantity()
    {
        var premix = Item("Premix", "g", stock: 40m, cost: 2m);
        var formula = new FeedFormula(Guid.NewGuid(), TenantId, "Premix", TargetAnimalType.Cattle);
        formula.AddIngredient(Linked(premix).Id, 100m, 2m, NutritionalProfile.Empty);

        var need = await Calculator().RequirementAsync(formula, 0.05m);

        need.Shortfalls().Should().ContainSingle().Which.Should().Contain("Required: 50");
    }

    // ── % normalisation ────────────────────────────────────────────────────

    [Fact]
    public async Task Percentages_AreNormalisedByTheirTotal()
    {
        var maize = Item("Maize", "kg", 1_000m, 30m);
        var straw = Unlinked("Straw", 6m);
        var formula = new FeedFormula(Guid.NewGuid(), TenantId, "Mix", TargetAnimalType.Cattle);
        formula.AddIngredient(Linked(maize).Id, 60m, 30m, NutritionalProfile.Empty);
        formula.AddIngredient(straw.Id, 30m, 6m, NutritionalProfile.Empty); // sums to 90

        var need = await Calculator().RequirementAsync(formula, 9m);

        need.Ingredients.Should().HaveCount(2);
        need.Ingredients[0].Kg.Should().Be(6m);
        need.Ingredients[1].Kg.Should().Be(3m);
        need.TotalCostBdt.Should().Be(6m * 30m + 3m * 6m);
    }

    [Theory]
    [InlineData(new double[] { 60, 40 }, true)]
    [InlineData(new double[] { 60, 39.995 }, true)]
    [InlineData(new double[] { 60, 30 }, false)]
    public void FormulaValidator_RequiresPercentagesToSumTo100(double[] percentages, bool valid)
    {
        var ingredients = new List<FormulaIngredientRequest>();
        foreach (var p in percentages) ingredients.Add(new FormulaIngredientRequest(Guid.NewGuid(), (decimal)p));

        var result = new CreateFeedFormulaCommandValidator().Validate(
            new CreateFeedFormulaCommand("Mix", TargetAnimalType.Cattle, Ingredients: ingredients));

        result.IsValid.Should().Be(valid);
    }

    // ── group-plan quantity basis ──────────────────────────────────────────

    private static AnimalFeedAllocation GroupRow(int heads, decimal kg = 3m, decimal cost = 90m) =>
        AnimalFeedAllocation.Create(TenantId, Guid.NewGuid(), FarmId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            new DateOnly(2026, 11, 1), kg, cost, 30m, FeedAllocationOrigin.GroupPlanShare, FeedAllocationMethod.EqualPerHead,
            300m, 1m / heads, heads, batchId: Guid.NewGuid());

    [Fact]
    public void GroupPlan_PerHeadBasis_MultipliesByHeads_WholeGroupDoesNot()
    {
        var plan = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            new DateOnly(2026, 11, 1), null, batchId: Guid.NewGuid());
        var rows = new[] { GroupRow(10), GroupRow(10) };

        FeedEntryHeads.Multiplier(plan, rows, GroupPlanQuantityBasis.PerHead).Should().Be(10m);
        FeedEntryHeads.Multiplier(plan, rows, GroupPlanQuantityBasis.WholeGroup).Should().Be(1m);

        var own = new AnimalFeedingPlan(Guid.NewGuid(), TenantId, FarmId, Guid.NewGuid(), FeedingPlanType.FixedQuantity,
            new DateOnly(2026, 11, 1), null, animalId: Guid.NewGuid());
        FeedEntryHeads.Multiplier(own, [], GroupPlanQuantityBasis.PerHead).Should().Be(1m);
    }

    // ── ml never counted as kg ─────────────────────────────────────────────

    [Theory]
    [InlineData("kg", true)]
    [InlineData("Kg", true)]
    [InlineData("g", true)]
    [InlineData("gm", true)]
    [InlineData("ml", false)]
    [InlineData("litre", false)]
    [InlineData("piece", false)]
    public void OnlyKgConvertibleUnits_FeedAsKg(string unit, bool asKg) =>
        FeedUnits.FeedsAsKg(unit).Should().Be(asKg);

    [Fact]
    public void ACustomUnitWithAKgConversion_FeedsAsKg() =>
        FeedUnits.FeedsAsKg("bag", ["Kg"]).Should().BeTrue();

    // ── ledger: set from confirmed totals, never accumulated ───────────────

    [Fact]
    public async Task LedgerSync_IsIdempotent_AdjustDoesNotDoubleCount()
    {
        var animalId = Guid.NewGuid();
        var allocations = Substitute.For<IAnimalFeedAllocationRepository>();
        allocations.GetTotalsForAnimalAsync(animalId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Farm360.Domain.Feeding.Interfaces.Repositories.AnimalFeedTotals(animalId, 10m, 300m, 1));
        var ledger = AnimalCostLedger.Create(TenantId, animalId, FarmId, 50_000m);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByAnimalIdAsync(animalId, Arg.Any<CancellationToken>()).Returns(ledger);

        await AnimalFeedCostSync.SyncAsync(allocations, ledgers, TenantId, FarmId, [animalId]);
        await AnimalFeedCostSync.SyncAsync(allocations, ledgers, TenantId, FarmId, [animalId]); // adjust / re-confirm

        ledger.TotalFeedCostBdt.Should().Be(300m);
    }

    [Fact]
    public async Task FinanceHandler_NoLongerAccumulatesFeedCost()
    {
        var animalId = Guid.NewGuid();
        var allocations = Substitute.For<IAnimalFeedAllocationRepository>();
        allocations.GetTotalsForAnimalAsync(animalId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Farm360.Domain.Feeding.Interfaces.Repositories.AnimalFeedTotals(animalId, 15m, 450m, 1));
        var ledger = AnimalCostLedger.Create(TenantId, animalId, FarmId, 0m);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByAnimalIdAsync(animalId, Arg.Any<CancellationToken>()).Returns(ledger);

        var handler = new DailyEntryConfirmedFinanceEventHandler(Substitute.For<ILedgerPostingService>(), ledgers,
            Substitute.For<IUnitOfWork>(), Substitute.For<ILogger<DailyEntryConfirmedFinanceEventHandler>>(), allocations);
        var evt = new DailyFeedingCostCalculatedEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), TenantId, FarmId,
            TotalCostBdt: 450m, ActualKg: 15m, EntryDate: new DateOnly(2026, 11, 1),
            AnimalId: animalId, BatchId: null, ShedId: null, StockCostBdt: 450m);

        await handler.Handle(evt, CancellationToken.None);
        await handler.Handle(evt, CancellationToken.None);

        ledger.TotalFeedCostBdt.Should().Be(450m);
    }

    // ── skip removes cost ──────────────────────────────────────────────────

    [Fact]
    public async Task Skip_ZeroesTheEntrysAllocations_AndResetsTheLedger()
    {
        var entry = new DailyFeedingEntry(Guid.NewGuid(), TenantId, Guid.NewGuid(), FarmId, new DateOnly(2026, 11, 1), Guid.NewGuid(), 3m);
        var row = GroupRow(1, kg: 3m, cost: 90m);
        var entries = Substitute.For<IDailyFeedingEntryRepository>();
        entries.GetByIdAsync(entry.Id, Arg.Any<CancellationToken>()).Returns(entry);
        var allocations = Substitute.For<IAnimalFeedAllocationRepository>();
        allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, Arg.Any<CancellationToken>()).Returns([row]);
        // After the skip the animal has no confirmed feed left.
        allocations.GetTotalsForAnimalAsync(row.AnimalId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new Farm360.Domain.Feeding.Interfaces.Repositories.AnimalFeedTotals(row.AnimalId, 0m, 0m, 0));
        var ledger = AnimalCostLedger.Create(TenantId, row.AnimalId, FarmId, 0m);
        ledger.UpdateFeedCost(90m);
        var ledgers = Substitute.For<IAnimalCostLedgerRepository>();
        ledgers.GetByAnimalIdAsync(row.AnimalId, Arg.Any<CancellationToken>()).Returns(ledger);

        var handler = new SkipDailyFeedingEntryCommandHandler(entries, Substitute.For<ILogger<SkipDailyFeedingEntryCommandHandler>>(),
            allocations, ledgers, Substitute.For<IUnitOfWork>());
        await handler.Handle(new SkipDailyFeedingEntryCommand(entry.Id, "No feed today"), CancellationToken.None);

        entry.Status.Should().Be(DailyFeedingEntryStatus.Skipped);
        row.AllocatedKg.Should().Be(0m);
        row.AllocatedCostBdt.Should().Be(0m);
        ledger.TotalFeedCostBdt.Should().Be(0m);
    }
}
