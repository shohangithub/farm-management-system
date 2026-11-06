using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Feeding.ValueObjects;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Feeding.Services;

/// <summary>One direct ration: a formula or a stock item, and how much of it per head per day.</summary>
/// <param name="KgPerDay">Kg of a recipe; for a stock item, the quantity in <paramref name="UnitId"/> (or its base unit).</param>
/// <param name="UnitId">For a stock item: the unit the quantity is entered in (ml, Bottle (100 ml), g…). Null = base unit.</param>
public sealed record DirectRation(Guid? FormulaId, Guid? InventoryItemId, decimal KgPerDay, FeedCategory? FeedType = null, Guid? UnitId = null);

/// <summary>A direct ration's daily amount as its rule line records it.</summary>
/// <param name="CostPerUnitBdt">For a stock item: its average cost per <paramref name="Unit"/>; 0 for a recipe.</param>
public sealed record FeedQuantity(decimal Quantity, string Unit, decimal CostPerUnitBdt);

public interface IDirectFeedService
{
    /// <summary>
    /// The formula that feeds a stock item as it is ("Extra Mineral (direct item)": one ingredient,
    /// 100%, linked to the item), created on first use. Feeding it deducts the item's stock and
    /// charges its average cost exactly like any other formula.
    /// </summary>
    Task<FeedFormula> GetOrCreateItemFormulaAsync(Guid inventoryItemId, TargetAnimalType species, CancellationToken cancellationToken = default);

    /// <summary>
    /// A fixed-quantity rule set for "this ration, this many kg a day" (e.g. "Direct · Extra Mineral
    /// (direct item) · 0.1 kg/day"), reused when an identical one already exists.
    /// </summary>
    Task<FeedingRuleSet> GetOrCreateDirectRuleSetAsync(DirectRation ration, TargetAnimalType species, CancellationToken cancellationToken = default);

    /// <summary>
    /// How much a day the ration's rule line records, and in what: kg for a recipe; for a stock item
    /// the entered unit turned into its base unit (2 Bottle (10 ml) → 20 ml), in kg when it converts to kg.
    /// </summary>
    Task<FeedQuantity> FeedQuantityAsync(DirectRation ration, CancellationToken cancellationToken = default);
}

/// <remarks>
/// Everything that consumes feed — daily entries, stock deduction, per-animal cost allocation,
/// reports — is keyed on a formula and a rule set. Rather than teach all of it a second path, a
/// stock item fed directly is wrapped in a one-ingredient formula, and a direct ration in a
/// one-line rule set. Both are ordinary records the user can see and edit.
/// Scoped: remembers what it created in this request, since unsaved rows don't come back from queries.
/// </remarks>
public sealed class DirectFeedService : IDirectFeedService
{
    public const string DirectItemSuffix = " (direct item)";
    public const string DirectRuleSetPrefix = "Direct · ";

    private readonly IInventoryItemRepository _inventory;
    private readonly IFeedIngredientRepository _ingredients;
    private readonly IFeedFormulaRepository _formulas;
    private readonly IFeedingRuleSetRepository _ruleSets;
    private readonly ITenantService _tenant;
    private readonly IUnitService? _units;

    private readonly Dictionary<Guid, FeedFormula> _itemFormulas = [];
    private readonly Dictionary<string, FeedingRuleSet> _directRuleSets = new(StringComparer.Ordinal);

    public DirectFeedService(
        IInventoryItemRepository inventory,
        IFeedIngredientRepository ingredients,
        IFeedFormulaRepository formulas,
        IFeedingRuleSetRepository ruleSets,
        ITenantService tenant,
        IUnitService? units = null)
    {
        _inventory = inventory;
        _ingredients = ingredients;
        _formulas = formulas;
        _ruleSets = ruleSets;
        _tenant = tenant;
        _units = units;
    }

    /// <summary>"Direct · Syp. Zinc (direct item) · 20 ml/day" → "ml"; kg for anything else.</summary>
    public static string UnitFromName(string ruleSetName)
    {
        ArgumentNullException.ThrowIfNull(ruleSetName);
        var slash = ruleSetName.LastIndexOf("/day", StringComparison.Ordinal);
        if (!ruleSetName.StartsWith(DirectRuleSetPrefix, StringComparison.Ordinal) || slash < 0) return "kg";
        var space = ruleSetName.LastIndexOf(' ', slash);
        return space < 0 || space + 1 >= slash ? "kg" : ruleSetName[(space + 1)..slash];
    }

    public async Task<FeedQuantity> FeedQuantityAsync(DirectRation ration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ration);
        if (ration.InventoryItemId is not { } itemId)
            return new FeedQuantity(ration.KgPerDay, "kg", 0m);

        var item = await _inventory.GetByIdAsync(itemId, cancellationToken)
            ?? throw new ArgumentException($"Stock item '{itemId}' was not found.");
        if (_units is null)
            return new FeedQuantity(ration.KgPerDay, item.UnitOfMeasure, item.WeightedAverageCostBdt);

        var inBase = (await _units.ToBaseAsync(item, ration.UnitId, ration.KgPerDay, 0m, cancellationToken)).BaseQuantity;
        var (quantity, unit) = await _units.ItemBaseToFeedAsync(item, inBase, cancellationToken);
        // Cost per recorded unit = cost per base unit × base units in one recorded unit.
        var cost = quantity > 0 ? item.WeightedAverageCostBdt * inBase / quantity : item.WeightedAverageCostBdt;
        return new FeedQuantity(quantity, unit, cost);
    }

    public async Task<FeedFormula> GetOrCreateItemFormulaAsync(Guid inventoryItemId, TargetAnimalType species, CancellationToken cancellationToken = default)
    {
        if (_itemFormulas.TryGetValue(inventoryItemId, out var cached))
            return cached;

        var item = await _inventory.GetByIdAsync(inventoryItemId, cancellationToken)
            ?? throw new ArgumentException($"Stock item '{inventoryItemId}' was not found.");
        if (item.Category == InventoryCategory.Equipment)
            throw new ArgumentException($"'{item.Name}' is equipment and cannot be fed.");

        var tenantId = _tenant.TenantId;

        // The feed ingredient that stands for this stock item (reuse the one linked to it, if any).
        var ingredient = (await _ingredients.GetAllAsync(tenantId, includePreloaded: false, cancellationToken))
            .FirstOrDefault(i => i.InventoryItemId == inventoryItemId);
        if (ingredient is null)
        {
            ingredient = new FeedIngredient(
                Guid.NewGuid(), tenantId, item.Name, GuessCategory(item.Name), NutritionalProfile.Empty,
                item.WeightedAverageCostBdt, item.UnitOfMeasure, description: "Linked to stock — created for direct feeding",
                inventoryItemId: inventoryItemId);
            await _ingredients.AddAsync(ingredient, cancellationToken);
        }

        var title = item.Name + DirectItemSuffix;
        var formula = (await _formulas.GetListAsync(tenantId, 1, 50, item.Name, cancellationToken))
            .FirstOrDefault(f => f.Ingredients.Count == 1 && f.Ingredients.First().IngredientId == ingredient.Id
                                 && f.Status != FormulaStatus.Archived);
        if (formula is null)
        {
            formula = new FeedFormula(Guid.NewGuid(), tenantId, title, species,
                description: "Feeds the stock item as it is (100%). Created automatically for direct feeding.");
            formula.AddIngredient(ingredient.Id, 100m, item.WeightedAverageCostBdt, ingredient.NutritionalProfile);
            formula.SetStatus(FormulaStatus.Active);
            await _formulas.AddAsync(formula, cancellationToken);
        }

        _itemFormulas[inventoryItemId] = formula;
        return formula;
    }

    public async Task<FeedingRuleSet> GetOrCreateDirectRuleSetAsync(DirectRation ration, TargetAnimalType species, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ration);
        if (ration.KgPerDay <= 0)
            throw new ArgumentException("Enter how much a day for each direct ration.");

        FeedFormula formula;
        if (ration.InventoryItemId is { } itemId)
        {
            formula = await GetOrCreateItemFormulaAsync(itemId, species, cancellationToken);
        }
        else if (ration.FormulaId is { } formulaId)
        {
            formula = await _formulas.GetByIdAsync(formulaId, cancellationToken)
                ?? throw new ArgumentException($"Formula '{formulaId}' was not found.");
        }
        else
        {
            throw new ArgumentException("Choose a formula or a stock item for each direct ration.");
        }

        // A stock item is given in its own unit (a liver tonic in ml); the line records it as feeding counts it.
        var amount = await FeedQuantityAsync(ration, cancellationToken);
        var unit = amount.Unit;
        var kg = Math.Round(amount.Quantity, 4);
        var name = $"{DirectRuleSetPrefix}{formula.Title} · {kg.ToString("0.####", CultureInfo.InvariantCulture)} {unit}/day";
        if (name.Length > 150) name = name[..150];

        if (_directRuleSets.TryGetValue(name, out var cached))
            return cached;

        var existing = (await _ruleSets.GetAllAsync(cancellationToken))
            .FirstOrDefault(r => r.Name == name && r.IsActive && r.PlanType == FeedingPlanType.FixedQuantity
                                 && r.Lines.Count == 1 && r.Lines.First().FormulaId == formula.Id
                                 && r.Lines.First().ConcentrateKgPerDay == kg);
        if (existing is not null)
        {
            _directRuleSets[name] = existing;
            return existing;
        }

        var ruleSet = new FeedingRuleSet(Guid.NewGuid(), _tenant.TenantId, name, species, FeedingPurpose.Growth,
            FeedingPlanType.FixedQuantity, baseNotes: "Direct ration — created from Assign Feeding Plan.");
        ruleSet.AddRuleLine(null, null, null, null, ration.FeedType ?? FeedCategory.Concentrate, kg, formula.Id);
        await _ruleSets.AddAsync(ruleSet, cancellationToken);

        _directRuleSets[name] = ruleSet;
        return ruleSet;
    }

    private static FeedCategory GuessCategory(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("mineral") || n.Contains("মিনারেল") || n.Contains("salt") || n.Contains("লবণ")) return FeedCategory.Mineral;
        if (n.Contains("vitamin") || n.Contains("ভিটামিন") || n.Contains("premix") || n.Contains("additive")
            || n.Contains("tonic") || n.Contains("টনিক") || n.Contains("zinc") || n.Contains("xinc") || n.Contains("জিংক")
            || n.Contains("amino") || n.Contains("অ্যামাইনো") || n.Contains("calcium") || n.Contains("ক্যালসিয়াম")) return FeedCategory.Additive;
        if (n.Contains("silage") || n.Contains("সাইলেজ")) return FeedCategory.Silage;
        if (n.Contains("straw") || n.Contains("খড়") || n.Contains("grass") || n.Contains("ঘাস") || n.Contains("hay")) return FeedCategory.Forage;
        return FeedCategory.Concentrate;
    }
}
