using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;

namespace Farm360.Application.Inventory.Units;

/// <summary>A quantity converted to the item's base unit, and what was actually entered.</summary>
/// <param name="BaseQuantity">In the item's base unit — what stock and costs use.</param>
/// <param name="BaseUnitCostBdt">Cost of one base unit (the entered rate ÷ the factor).</param>
/// <param name="EnteredUnitName">The unit typed in, or null when it was the base unit itself.</param>
public sealed record UnitQuantity(decimal BaseQuantity, decimal BaseUnitCostBdt, string? EnteredUnitName, decimal EnteredQuantity, decimal Factor);

public interface IUnitService
{
    /// <summary>The tenant's base units and their conversions, creating the usual farm set the first time.</summary>
    Task<IReadOnlyList<MeasureUnit>> GetUnitsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a quantity (and its rate) entered in <paramref name="unitId"/> to the item's base unit.
    /// No unit means the quantity is already in the base unit.
    /// </summary>
    Task<UnitQuantity> ToBaseAsync(InventoryItem item, Guid? unitId, decimal quantity, decimal ratePerUnit = 0m, CancellationToken cancellationToken = default);

    /// <summary>Kilograms of feed in the item's base unit (a gram-based item takes kg × 1000).</summary>
    Task<decimal> KgToItemBaseAsync(InventoryItem item, decimal kg, CancellationToken cancellationToken = default);

    /// <summary>The conversions an item can be entered in: those of its base unit.</summary>
    Task<IReadOnlyList<UnitConversion>> ConversionsForAsync(string baseUnitName, CancellationToken cancellationToken = default);

    /// <summary>The base unit an item is counted in (by its BaseUnitId link).</summary>
    Task<MeasureUnit?> BaseUnitOfAsync(InventoryItem item, CancellationToken cancellationToken = default);

    /// <summary>The base unit called <paramref name="name"/> (usual spellings allowed), created if there is none.</summary>
    Task<MeasureUnit> ResolveBaseUnitAsync(string name, CancellationToken cancellationToken = default);
}

public sealed class UnitService(
    IMeasureUnitRepository units,
    ITenantService tenant,
    IUnitOfWork unitOfWork,
    IInventoryItemRepository? items = null) : IUnitService
{
    private static readonly string[] KgNames = ["kg", "kgs", "kilogram", "kilograms", "কেজি"];

    /// <summary>Base unit → (conversion name, base units in one).</summary>
    private static readonly (string Base, string Description, (string Name, decimal Factor)[] Conversions)[] Defaults =
    [
        ("kg", "Kilogram — feed, grain, minerals", [("Gram", 0.001m), ("Bag (25 kg)", 25m), ("Bag (40 kg)", 40m), ("Bag (50 kg)", 50m), ("Maund (40 kg)", 40m)]),
        ("g", "Gram — powders, premix", [("Kg", 1000m), ("Packet (100 g)", 100m), ("Packet (500 g)", 500m)]),
        ("litre", "Litre — liquids", [("ml", 0.001m), ("Can (5 L)", 5m)]),
        ("ml", "Millilitre — injections, liquid medicine", [("Litre", 1000m), ("Bottle (10 ml)", 10m), ("Bottle (100 ml)", 100m)]),
        ("piece", "Piece — tablets, bolus, items", [("Strip (10)", 10m), ("Box (10)", 10m), ("Box (100)", 100m)]),
        ("dose", "Dose — vaccines", [("Vial (10 doses)", 10m), ("Vial (50 doses)", 50m)]),
    ];

    private List<MeasureUnit>? _cache;

    public async Task<IReadOnlyList<MeasureUnit>> GetUnitsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
            return _cache;

        var all = (await units.GetAllAsync(cancellationToken)).ToList();
        if (all.Count == 0)
        {
            foreach (var (name, description, conversions) in Defaults)
            {
                var unit = new MeasureUnit(Guid.NewGuid(), tenant.TenantId, name, description);
                foreach (var (cName, factor) in conversions)
                    unit.AddConversion(cName, factor);
                await units.AddAsync(unit, cancellationToken);
                all.Add(unit);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        _cache = all;
        await LinkItemsAsync(cancellationToken);
        return _cache;
    }

    /// <summary>
    /// Items created before units existed only carry a unit name: link each to its base unit
    /// (matching the usual spellings), creating a base unit for a name that has none. Runs until
    /// every item has a link, so stock is always tied to a real base unit.
    /// </summary>
    private async Task LinkItemsAsync(CancellationToken cancellationToken)
    {
        if (items is null || _linked)
            return;
        _linked = true;

        var unlinked = await items.GetWithoutBaseUnitAsync(cancellationToken);
        if (unlinked.Count == 0)
            return;

        foreach (var item in unlinked)
        {
            var unit = await ResolveBaseUnitAsync(item.UnitOfMeasure, cancellationToken);
            item.SetBaseUnit(unit.Id, unit.Name);
            items.Update(item);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private bool _linked;

    public async Task<MeasureUnit> ResolveBaseUnitAsync(string name, CancellationToken cancellationToken = default)
    {
        var all = _cache ?? (await units.GetAllAsync(cancellationToken)).ToList();
        _cache ??= all;
        var found = all.FirstOrDefault(u => SameUnit(u.Name, name));
        if (found is not null)
            return found;

        var unit = new MeasureUnit(Guid.NewGuid(), tenant.TenantId, string.IsNullOrWhiteSpace(name) ? "unit" : name.Trim(),
            "Created from an item's unit");
        await units.AddAsync(unit, cancellationToken);
        _cache.Add(unit);
        return unit;
    }

    public async Task<MeasureUnit?> BaseUnitOfAsync(InventoryItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var all = await GetUnitsAsync(cancellationToken);
        return item.BaseUnitId is { } id
            ? all.FirstOrDefault(u => u.Id == id)
            : all.FirstOrDefault(u => SameUnit(u.Name, item.UnitOfMeasure));
    }

    public async Task<IReadOnlyList<UnitConversion>> ConversionsForAsync(string baseUnitName, CancellationToken cancellationToken = default)
    {
        var unit = await FindBaseAsync(baseUnitName, cancellationToken);
        return unit is null ? [] : unit.Conversions.Where(c => c.IsActive).OrderBy(c => c.Factor).ToList();
    }

    public async Task<UnitQuantity> ToBaseAsync(InventoryItem item, Guid? unitId, decimal quantity, decimal ratePerUnit = 0m, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (unitId is null)
            return new UnitQuantity(quantity, ratePerUnit, null, quantity, 1m);

        var owner = (await GetUnitsAsync(cancellationToken)).FirstOrDefault(u => u.Conversions.Any(c => c.Id == unitId))
            ?? throw new ArgumentException("The selected unit was not found.");
        var conversion = owner.Conversions.First(c => c.Id == unitId);

        var itemBase = await BaseUnitOfAsync(item, cancellationToken);
        if (itemBase is null || itemBase.Id != owner.Id)
            throw new ArgumentException($"'{conversion.Name}' is a unit of {owner.Name}, but {item.Name} is counted in {item.UnitOfMeasure}.");
        if (!conversion.IsActive)
            throw new ArgumentException($"'{conversion.Name}' is inactive.");

        // Stock is kept in the base unit: quantity × conversion value. The base unit itself is not recorded as "entered".
        var isBase = owner.IsSelf(conversion);
        return new UnitQuantity(
            conversion.ToBase(quantity),
            conversion.Factor == 0 ? ratePerUnit : ratePerUnit / conversion.Factor,
            isBase ? null : conversion.Name,
            quantity,
            conversion.Factor);
    }

    public async Task<decimal> KgToItemBaseAsync(InventoryItem item, decimal kg, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (KgNames.Any(n => SameUnit(n, item.UnitOfMeasure)))
            return kg;

        var unit = await BaseUnitOfAsync(item, cancellationToken);
        var kgConversion = unit?.Conversions.FirstOrDefault(c => KgNames.Any(n => SameUnit(n, c.Name)));
        // No way to turn kg into this unit: keep the old behaviour (treat it as kg).
        return kgConversion is null ? kg : kgConversion.ToBase(kg);
    }

    private async Task<MeasureUnit?> FindBaseAsync(string name, CancellationToken ct) =>
        (await GetUnitsAsync(ct)).FirstOrDefault(u => SameUnit(u.Name, name));

    /// <summary>
    /// Same unit, allowing the usual spellings — items created before units existed say "Kg",
    /// "gm", "pcs" or "লিটার" and should still find their base unit and packs.
    /// </summary>
    public static bool SameUnit(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kg"] = "kg", ["kgs"] = "kg", ["kilogram"] = "kg", ["kilograms"] = "kg", ["কেজি"] = "kg",
        ["g"] = "g", ["gm"] = "g", ["gms"] = "g", ["gram"] = "g", ["grams"] = "g", ["গ্রাম"] = "g",
        ["l"] = "litre", ["ltr"] = "litre", ["liter"] = "litre", ["litre"] = "litre", ["liters"] = "litre", ["litres"] = "litre", ["লিটার"] = "litre",
        ["ml"] = "ml", ["millilitre"] = "ml", ["milliliter"] = "ml", ["মিলি"] = "ml",
        ["pc"] = "piece", ["pcs"] = "piece", ["piece"] = "piece", ["pieces"] = "piece", ["nos"] = "piece", ["টি"] = "piece", ["পিস"] = "piece",
        ["dose"] = "dose", ["doses"] = "dose", ["ডোজ"] = "dose",
    };

    private static string Normalize(string? unit)
    {
        var u = (unit ?? string.Empty).Trim();
        return Aliases.TryGetValue(u, out var canonical) ? canonical : u.ToLowerInvariant();
    }
}

/// <summary>Links an item to its base unit and default (purchase) unit, by id — as FrostTrack's Product → UnitConversion → BaseUnit.</summary>
public static class ItemUnitLinker
{
    /// <param name="baseUnitId">Chosen base unit; when null the unit name is matched (or created).</param>
    /// <param name="defaultUnitId">Usual purchase / issue unit; must be a conversion of the base unit.</param>
    public static async Task ApplyAsync(IUnitService units, InventoryItem item, Guid? baseUnitId, string unitName, Guid? defaultUnitId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(item);

        MeasureUnit unit;
        if (baseUnitId is { } id)
        {
            unit = (await units.GetUnitsAsync(cancellationToken)).FirstOrDefault(u => u.Id == id)
                ?? throw new ArgumentException("The selected base unit was not found.");
        }
        else
        {
            unit = await units.ResolveBaseUnitAsync(unitName, cancellationToken);
        }

        item.SetBaseUnit(unit.Id, unit.Name);

        if (defaultUnitId is { } du && unit.Conversions.All(c => c.Id != du))
            throw new ArgumentException($"The usual unit must be one of {unit.Name}'s conversions.");
        item.SetDefaultUnit(defaultUnitId);
    }
}
