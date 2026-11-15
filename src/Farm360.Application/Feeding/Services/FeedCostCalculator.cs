using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Feeding.Services;

/// <summary>One recipe ingredient's part of a quantity of feed.</summary>
/// <param name="Share">Fraction of one kg of the recipe (percentage ÷ the recipe's percentage total).</param>
/// <param name="Kg">Kg of this ingredient (or the item's own unit — ml — for a liquid fed as it is).</param>
/// <param name="Item">The stock item it is drawn from; null when it is not stock-tracked.</param>
/// <param name="StockQuantity">The same quantity in the stock item's base unit (kg × 1000 for a gram item).</param>
/// <param name="CostPerKgBdt">Cost of one kg (one feed unit) of this ingredient.</param>
public sealed record IngredientRequirement(
    Guid IngredientId,
    string Name,
    decimal Percentage,
    decimal Share,
    decimal Kg,
    InventoryItem? Item,
    decimal StockQuantity,
    decimal CostPerKgBdt)
{
    public decimal CostBdt => Kg * CostPerKgBdt;

    public bool IsStockTracked => Item is not null;

    public bool IsShort => Item is { } item && StockQuantity > 0 && (!item.IsActive || item.CurrentStock < StockQuantity);
}

/// <summary>What a quantity of a recipe takes out of stock and what it costs.</summary>
public sealed record FeedRequirement(Guid FormulaId, decimal TotalKg, IReadOnlyList<IngredientRequirement> Ingredients)
{
    public decimal TotalCostBdt => Ingredients.Sum(i => i.CostBdt);

    public decimal CostPerKgBdt => TotalKg > 0 ? TotalCostBdt / TotalKg : 0m;

    /// <summary>One message per stock item that can't cover its part (empty when everything is in stock).</summary>
    public IReadOnlyList<string> Shortfalls() =>
        Ingredients.Where(i => i.IsShort)
            .Select(i => $"Insufficient stock for '{i.Item!.Name}'. Required: {i.StockQuantity:0.##} {i.Item.UnitOfMeasure}, Available: {i.Item.CurrentStock:0.##} {i.Item.UnitOfMeasure}.")
            .ToList();
}

/// <summary>
/// The single place feed is priced and split into stock. Feed quantities are in kg; a stock item's
/// weighted-average cost is per its base unit (a premix kept in grams costs BDT per gram), so the
/// cost of a kg of it is cost per base unit × base units in a kg (2 BDT/g → 2000 BDT/kg). Recipe
/// percentages are normalised by their total (<see cref="FeedFormula.ShareOf"/>). Confirmation
/// pre-checks, stock deduction, the daily job, planning and the cost breakdown all use it, so they
/// can't disagree.
/// </summary>
/// <remarks>A plain helper rather than a DI service: built from repositories the caller already has. Caches per instance.</remarks>
public sealed class FeedCostCalculator
{
    private readonly IFeedIngredientRepository _ingredients;
    private readonly IInventoryItemRepository _inventory;
    private readonly IUnitService? _units;

    private readonly Dictionary<Guid, FeedIngredient?> _ingredientCache = [];
    private readonly Dictionary<Guid, InventoryItem?> _itemCache = [];
    private readonly Dictionary<Guid, decimal> _basePerKgCache = [];
    private readonly Dictionary<Guid, decimal> _formulaCostCache = [];

    public FeedCostCalculator(IFeedIngredientRepository ingredients, IInventoryItemRepository inventory, IUnitService? units)
    {
        _ingredients = ingredients;
        _inventory = inventory;
        _units = units;
    }

    /// <summary>Seeds the look-ups with ingredients and items the caller already loaded (planning queries).</summary>
    public FeedCostCalculator(
        IFeedIngredientRepository ingredients,
        IInventoryItemRepository inventory,
        IUnitService? units,
        IEnumerable<FeedIngredient> knownIngredients,
        IEnumerable<InventoryItem> knownItems)
        : this(ingredients, inventory, units)
    {
        ArgumentNullException.ThrowIfNull(knownIngredients);
        ArgumentNullException.ThrowIfNull(knownItems);
        foreach (var i in knownIngredients) _ingredientCache[i.Id] = i;
        foreach (var i in knownItems) _itemCache[i.Id] = i;
    }

    /// <summary>Cost of a kg of feed given a stock item's cost per base unit and its base units per kg.</summary>
    public static decimal CostPerKg(decimal costPerBaseUnitBdt, decimal baseUnitsPerKg) =>
        costPerBaseUnitBdt * (baseUnitsPerKg > 0 ? baseUnitsPerKg : 1m);

    /// <summary>Base units of the item in one kg of feed (1000 for a gram item; 1 for kg — or for ml, which feeds as its own unit).</summary>
    public async Task<decimal> BaseUnitsPerKgAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_units is null)
            return 1m;
        if (_basePerKgCache.TryGetValue(item.Id, out var cached))
            return cached;
        var perKg = await _units.KgToItemBaseAsync(item, 1m, cancellationToken).ConfigureAwait(false);
        if (perKg <= 0) perKg = 1m;
        _basePerKgCache[item.Id] = perKg;
        return perKg;
    }

    /// <summary>Cost of a kg (feed unit) of a stock item at its current weighted-average cost.</summary>
    public async Task<decimal> StockItemCostPerKgAsync(InventoryItem item, CancellationToken cancellationToken = default) =>
        CostPerKg(item.WeightedAverageCostBdt, await BaseUnitsPerKgAsync(item, cancellationToken).ConfigureAwait(false));

    /// <summary>A recipe's current cost per kg: live stock costs for linked ingredients, the recipe's own for the rest.</summary>
    public async Task<decimal> FormulaCostPerKgAsync(FeedFormula? formula, CancellationToken cancellationToken = default)
    {
        if (formula is null)
            return 0m;
        if (_formulaCostCache.TryGetValue(formula.Id, out var cached))
            return cached;
        var cost = formula.Ingredients.Count == 0
            ? formula.TotalCostPerKgBdt
            : (await RequirementAsync(formula, 1m, cancellationToken).ConfigureAwait(false)).TotalCostBdt;
        _formulaCostCache[formula.Id] = cost;
        return cost;
    }

    /// <summary>Splits <paramref name="totalKg"/> of a recipe into its ingredients, stock quantities and costs.</summary>
    public async Task<FeedRequirement> RequirementAsync(FeedFormula formula, decimal totalKg, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(formula);
        var lines = new List<IngredientRequirement>(formula.Ingredients.Count);
        foreach (var fi in formula.Ingredients)
        {
            var share = formula.ShareOf(fi);
            // Multiply before dividing so 9 kg of 60/90 is exactly 6 kg, not 6.0000…03.
            var pctTotal = formula.PercentageTotal;
            var kg = pctTotal > 0 ? totalKg * fi.Percentage / pctTotal : totalKg * share;
            var ingredient = await IngredientAsync(fi.IngredientId, cancellationToken).ConfigureAwait(false);
            var costPerKg = fi.IngredientCostPerKg;
            InventoryItem? item = null;
            var stockQty = kg;

            if (ingredient?.InventoryItemId is { } itemId)
            {
                item = await ItemAsync(itemId, cancellationToken).ConfigureAwait(false);
                if (item is not null)
                {
                    var basePerKg = await BaseUnitsPerKgAsync(item, cancellationToken).ConfigureAwait(false);
                    stockQty = kg * basePerKg;
                    if (item.WeightedAverageCostBdt > 0)
                        costPerKg = CostPerKg(item.WeightedAverageCostBdt, basePerKg);
                }
            }

            lines.Add(new IngredientRequirement(fi.IngredientId, ingredient?.Name ?? "Unknown ingredient", fi.Percentage, share,
                kg, item, stockQty, costPerKg));
        }

        return new FeedRequirement(formula.Id, totalKg, lines);
    }

    private async Task<FeedIngredient?> IngredientAsync(Guid id, CancellationToken ct)
    {
        if (_ingredientCache.TryGetValue(id, out var cached))
            return cached;
        var found = await _ingredients.GetByIdAsync(id, ct).ConfigureAwait(false);
        _ingredientCache[id] = found;
        return found;
    }

    private async Task<InventoryItem?> ItemAsync(Guid id, CancellationToken ct)
    {
        if (_itemCache.TryGetValue(id, out var cached))
            return cached;
        var found = await _inventory.GetByIdAsync(id, ct).ConfigureAwait(false);
        _itemCache[id] = found;
        return found;
    }
}

/// <summary>Heads a feeding entry's quantity covers, honouring <see cref="GroupPlanQuantityBasis"/>.</summary>
public static class FeedEntryHeads
{
    public static decimal Multiplier(AnimalFeedingPlan? plan, IReadOnlyList<AnimalFeedAllocation> allocations, GroupPlanQuantityBasis basis)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        var group = allocations.FirstOrDefault(a => a.Origin != FeedAllocationOrigin.IndividualPlan);
        var isGroup = plan is null ? group is not null : !plan.AnimalId.HasValue;
        return GroupFeedQuantity.HeadMultiplier(isGroup, group?.HeadCountAtAllocation ?? 1, basis);
    }

    /// <summary>Loads the plan and allocations an entry needs to answer <see cref="Multiplier"/>.</summary>
    public static async Task<decimal> MultiplierAsync(
        DailyFeedingEntry entry,
        IAnimalFeedingPlanRepository? plans,
        IAnimalFeedAllocationRepository? allocations,
        GroupPlanQuantityBasis basis,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (plans is null || allocations is null)
            return 1m;
        var plan = await plans.GetByIdAsync(entry.FeedingPlanId, cancellationToken).ConfigureAwait(false);
        var rows = await allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        return Multiplier(plan, rows, basis);
    }
}

/// <summary>
/// Sets each animal's ledger feed bucket to its confirmed allocation total — idempotent, so a
/// confirmation, adjustment, skip or resync can run any number of times without double counting.
/// The caller saves.
/// </summary>
public static class AnimalFeedCostSync
{
    public static async Task<int> SyncAsync(
        IAnimalFeedAllocationRepository allocations,
        IAnimalCostLedgerRepository ledgers,
        Guid tenantId,
        Guid farmId,
        IEnumerable<Guid> animalIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(ledgers);
        ArgumentNullException.ThrowIfNull(animalIds);

        var changed = 0;
        foreach (var animalId in animalIds.Where(id => id != Guid.Empty).Distinct())
        {
            var totals = await allocations.GetTotalsForAnimalAsync(animalId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);
            var feedCost = Math.Max(0m, totals.TotalCostBdt);
            var ledger = await ledgers.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
            if (ledger is null)
            {
                if (feedCost <= 0) continue;
                ledger = AnimalCostLedger.Create(tenantId, animalId, farmId, 0m);
                ledger.UpdateFeedCost(feedCost);
                ledgers.Add(ledger);
                changed++;
            }
            else if (ledger.TotalFeedCostBdt != Math.Round(feedCost, 2))
            {
                ledger.UpdateFeedCost(feedCost);
                ledgers.Update(ledger);
                changed++;
            }
        }

        return changed;
    }
}

/// <summary>Whether a stock item's quantities feed as kilograms.</summary>
public static class FeedUnits
{
    /// <summary>
    /// True for kg, g, or a base unit with a kg conversion — exactly the items
    /// <see cref="Inventory.Units.IUnitService.KgToItemBaseAsync"/> converts. Anything else (ml,
    /// pieces) is fed and recorded in its own unit, so it must never be added into kg totals.
    /// </summary>
    public static bool FeedsAsKg(string? unitOfMeasure, IEnumerable<string>? conversionNames = null) =>
        UnitService.SameUnit(unitOfMeasure, "kg")
        || UnitService.SameUnit(unitOfMeasure, "g")
        || (conversionNames?.Any(n => UnitService.SameUnit(n, "kg")) ?? false);
}
