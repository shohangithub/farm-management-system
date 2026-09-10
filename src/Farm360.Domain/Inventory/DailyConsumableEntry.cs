using Farm360.Domain.Common;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Exceptions;

namespace Farm360.Domain.Inventory;

public class DailyConsumableEntry : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public Guid ConsumableUsagePlanId { get; private set; }
    public Guid ConsumableUsagePlanItemId { get; private set; }
    public Guid InventoryItemId { get; private set; }
    public DateOnly EntryDate { get; private set; }
    public decimal ExpectedQuantity { get; private set; }
    public decimal? ActualQuantity { get; private set; }
    public decimal? UnitCostAtConsumptionBdt { get; private set; }
    public decimal? TotalCostBdt { get; private set; }
    public DailyConsumableEntryStatus Status { get; private set; } = DailyConsumableEntryStatus.Pending;
    public string? AdjustmentReason { get; private set; }
    public Guid? InventoryTransactionId { get; private set; }

    private DailyConsumableEntry() { }

    public DailyConsumableEntry(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid consumableUsagePlanId,
        Guid consumableUsagePlanItemId,
        Guid inventoryItemId,
        DateOnly entryDate,
        decimal expectedQuantity)
        : base(id, tenantId)
    {
        if (farmId == Guid.Empty)
            throw new InventoryDomainException("FarmId cannot be empty.");
        if (inventoryItemId == Guid.Empty)
            throw new InventoryDomainException("InventoryItemId cannot be empty.");
        if (expectedQuantity <= 0)
            throw new InventoryDomainException("Expected quantity must be greater than zero.");

        FarmId = farmId;
        ConsumableUsagePlanId = consumableUsagePlanId;
        ConsumableUsagePlanItemId = consumableUsagePlanItemId;
        InventoryItemId = inventoryItemId;
        EntryDate = entryDate;
        ExpectedQuantity = expectedQuantity;
        Status = DailyConsumableEntryStatus.Pending;
    }

    public void Confirm(decimal actualQuantity, decimal unitCostBdt, Guid? inventoryTransactionId = null, string? adjustmentReason = null)
    {
        if (Status == DailyConsumableEntryStatus.Confirmed || Status == DailyConsumableEntryStatus.Adjusted)
            return;

        if (actualQuantity < 0)
            throw new InventoryDomainException("Actual quantity cannot be negative.");

        ActualQuantity = actualQuantity;
        UnitCostAtConsumptionBdt = Math.Max(0, unitCostBdt);
        TotalCostBdt = Math.Round(actualQuantity * UnitCostAtConsumptionBdt.Value, 2);
        InventoryTransactionId = inventoryTransactionId;
        AdjustmentReason = adjustmentReason?.Trim();

        Status = actualQuantity == ExpectedQuantity 
            ? DailyConsumableEntryStatus.Confirmed 
            : DailyConsumableEntryStatus.Adjusted;
    }

    public void Skip(string reason)
    {
        if (Status == DailyConsumableEntryStatus.Confirmed || Status == DailyConsumableEntryStatus.Adjusted)
            throw new InventoryDomainException("Cannot skip an already confirmed entry.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new InventoryDomainException("Reason must be provided when skipping an entry.");

        Status = DailyConsumableEntryStatus.Skipped;
        AdjustmentReason = reason.Trim();
        ActualQuantity = 0;
        TotalCostBdt = 0;
    }
}
