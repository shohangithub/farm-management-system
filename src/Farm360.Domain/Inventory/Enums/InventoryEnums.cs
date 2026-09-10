namespace Farm360.Domain.Inventory.Enums;

public enum InventoryCategory
{
    Feed = 1,
    Medicine = 2,
    Vaccine = 3,
    Chemical = 4,
    Equipment = 5,
    Other = 6,
    Consumable = 7
}

public enum StockTransactionType
{
    StockIn = 1,
    ManualStockOut = 2,
    AutoFeedConsumption = 3,
    AutoMedicineConsumption = 4,
    Adjustment = 5,
    WriteOff = 6,
    PlannedFeedConsumption = 7,
    ReconciliationAdjustment = 8,
    AutoConsumableUsage = 9
}

public enum ConsumableUsagePlanStatus
{
    Active = 1,
    Paused = 2,
    Completed = 3
}

public enum DailyConsumableEntryStatus
{
    Pending = 1,
    Confirmed = 2,
    Adjusted = 3,
    Skipped = 4
}

public enum InventoryStatus
{
    Sufficient = 1,
    LowStock = 2,
    OutOfStock = 3,
    Excess = 4
}

public enum PurchaseOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Fulfilled = 4,
    Cancelled = 5
}
