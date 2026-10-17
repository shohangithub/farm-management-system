using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

/// <summary>
/// A base unit stock is counted in — kg, g, litre, ml, piece. An inventory item's
/// <see cref="InventoryItem.UnitOfMeasure"/> names its base unit: stock, weighted average cost and
/// every consumption are always kept in it.
/// </summary>
public sealed class MeasureUnit : AuditableEntity, IAggregateRoot
{
    private readonly List<UnitConversion> _conversions = new();

    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<UnitConversion> Conversions => _conversions.AsReadOnly();

    private MeasureUnit() { } // EF Core

    public MeasureUnit(Guid id, Guid tenantId, string name, string? description = null) : base(id, tenantId)
    {
        Rename(name, description);
        // Every base unit can always be entered as itself.
        _conversions.Add(new UnitConversion(Guid.NewGuid(), tenantId, Id, Name, 1m));
    }

    public void Rename(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Unit name is required.");
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public void SetActive(bool active) => IsActive = active;

    /// <summary>
    /// Adds a pack or alternative unit, e.g. "Bag (50 kg)" = 50 kg. <paramref name="factor"/> is how
    /// many base units one of it holds.
    /// </summary>
    public UnitConversion AddConversion(string name, decimal factor, string? description = null)
    {
        if (_conversions.Any(c => string.Equals(c.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InventoryDomainException($"'{name}' already exists for {Name}.");
        var conversion = new UnitConversion(Guid.NewGuid(), TenantId, Id, name, factor);
        conversion.Update(name, factor, description);
        _conversions.Add(conversion);
        return conversion;
    }

    /// <summary>The conversion that is this base unit itself (factor 1, same name).</summary>
    public bool IsSelf(UnitConversion conversion) =>
        conversion.Factor == 1m && string.Equals(conversion.Name, Name, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A unit stock can be bought, issued or used in — "Bag (50 kg)", "Gram", "Bottle (100 ml)" — and
/// how many base units one of it holds (<see cref="Factor"/>). Quantities entered in it are
/// converted to the base unit before they touch stock.
/// </summary>
public sealed class UnitConversion : AuditableEntity
{
    public Guid MeasureUnitId { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>Base units in one of this unit (Bag 50 kg → 50; Gram on a kg base → 0.001).</summary>
    public decimal Factor { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; } = true;

    private UnitConversion() { } // EF Core

    internal UnitConversion(Guid id, Guid tenantId, Guid measureUnitId, string name, decimal factor) : base(id, tenantId)
    {
        MeasureUnitId = measureUnitId;
        Update(name, factor);
    }

    public void Update(string name, decimal factor, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InventoryDomainException("Unit name is required.");
        if (factor <= 0)
            throw new InventoryDomainException("The conversion must be greater than zero.");
        Name = name.Trim();
        Factor = factor;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Re-files the conversion under another base unit (FrostTrack lets the base unit be changed on edit).</summary>
    public void MoveTo(Guid measureUnitId) => MeasureUnitId = measureUnitId;

    public void SetActive(bool active) => IsActive = active;

    public decimal ToBase(decimal quantity) => quantity * Factor;

    public decimal FromBase(decimal baseQuantity) => baseQuantity / Factor;
}
