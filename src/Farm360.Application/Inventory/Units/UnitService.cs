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
}

public sealed class UnitService(IMeasureUnitRepository units, ITenantService tenant, IUnitOfWork unitOfWork) : IUnitService
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
        return all;
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

        if (!SameUnit(owner.Name, item.UnitOfMeasure))
            throw new ArgumentException($"'{conversion.Name}' is a unit of {owner.Name}, but {item.Name} is counted in {item.UnitOfMeasure}.");

        // The base unit itself (factor 1, same name) is stored without an "entered" unit.
        var isBase = conversion.Factor == 1m && SameUnit(conversion.Name, owner.Name);
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

        var unit = await FindBaseAsync(item.UnitOfMeasure, cancellationToken);
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
