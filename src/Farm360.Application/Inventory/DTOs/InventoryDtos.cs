using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Inventory.DTOs;

public sealed record InventoryItemDto(
    Guid Id,
    Guid FarmId,
    string Name,
    string Sku,
    InventoryCategory Category,
    string CategoryName,
    string UnitOfMeasure,
    decimal ReorderThreshold,
    decimal CurrentStock,
    decimal WeightedAverageCostBdt,
    decimal TotalValueBdt,
    InventoryStatus Status,
    string StatusName,
    string? StorageLocation,
    bool IsActive)
{
    /// <summary>The unit it is usually bought or issued in (e.g. "Bag (50 kg)").</summary>
    public Guid? DefaultUnitId { get; init; }
}

public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? ContactPerson,
    string? Phone,
    string? Email,
    string? Address,
    string? Notes,
    bool IsActive);

public sealed record StockTransactionDto(
    Guid Id,
    Guid FarmId,
    Guid InventoryItemId,
    string ItemName,
    StockTransactionType TransactionType,
    string TransactionTypeName,
    decimal Quantity,
    decimal UnitCostBdt,
    decimal TotalCostBdt,
    decimal BalanceAfter,
    DateOnly TransactionDate,
    Guid? SupplierId,
    string? SupplierName,
    string? InvoiceNumber,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    string? Reason,
    string? RecordedBy)
{
    /// <summary>The unit it was entered in ("Bag (50 kg)") and that quantity; Quantity is in the base unit.</summary>
    public string? EnteredUnitName { get; init; }
    public decimal? EnteredQuantity { get; init; }
}

public sealed record InventoryValuationReportDto(
    Guid FarmId,
    decimal TotalValuationBdt,
    int TotalSkusCount,
    int LowStockCount,
    int OutOfStockCount,
    IReadOnlyList<InventoryItemDto> Items);

public sealed record CurrentStockSummaryDto(
    int TotalItems,
    decimal TotalValueBdt,
    int LowStockCount,
    int OutOfStockCount);
