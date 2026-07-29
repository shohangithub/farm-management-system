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
    WriteOff = 6
}

public enum InventoryStatus
{
    Sufficient = 1,
    LowStock = 2,
    OutOfStock = 3,
    Excess = 4
}
