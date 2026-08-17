using Farm360.Domain.Common;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.ValueObjects;

namespace Farm360.Domain.Feeding;

public sealed class FeedIngredient : AuditableEntity, IAggregateRoot
{
    public string Name { get; private set; } = null!;
    public FeedCategory Category { get; private set; }
    public NutritionalProfile NutritionalProfile { get; private set; } = null!;
    public string Unit { get; private set; } = "kg";
    public decimal UnitCostBdt { get; private set; }
    public bool IsPreloaded { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }
    public Guid? InventoryItemId { get; private set; }

    private FeedIngredient() { } // EF Core

    public FeedIngredient(
        Guid id,
        Guid tenantId,
        string name,
        FeedCategory category,
        NutritionalProfile nutritionalProfile,
        decimal unitCostBdt,
        string unit = "kg",
        bool isPreloaded = false,
        string? description = null,
        Guid? inventoryItemId = null)
        : base(id, tenantId)
    {
        Name = name;
        Category = category;
        NutritionalProfile = nutritionalProfile;
        UnitCostBdt = Math.Max(0, unitCostBdt);
        Unit = string.IsNullOrWhiteSpace(unit) ? "kg" : unit;
        IsPreloaded = isPreloaded;
        IsActive = true;
        Description = description;
        InventoryItemId = inventoryItemId;
    }

    public void UpdateDetails(
        string name,
        FeedCategory category,
        NutritionalProfile nutritionalProfile,
        decimal unitCostBdt,
        string unit = "kg",
        string? description = null,
        Guid? inventoryItemId = null)
    {
        Name = name;
        Category = category;
        NutritionalProfile = nutritionalProfile;
        UnitCostBdt = Math.Max(0, unitCostBdt);
        Unit = string.IsNullOrWhiteSpace(unit) ? "kg" : unit;
        Description = description;
        InventoryItemId = inventoryItemId;
    }

    public void UpdateCost(decimal newCostBdt)
    {
        UnitCostBdt = Math.Max(0, newCostBdt);
    }

    public void SetActiveStatus(bool active)
    {
        IsActive = active;
    }
}
