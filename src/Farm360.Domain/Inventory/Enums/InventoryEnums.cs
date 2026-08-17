namespace Farm360.Domain.Inventory.Enums;

public enum InventoryCategory
{
    Feed = 1,
    Medicine = 2,
    Vaccine = 3,
    Chemical = 4,
    Equipment = 5,
    Other = 6
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
    ReconciliationAdjustment = 8
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
