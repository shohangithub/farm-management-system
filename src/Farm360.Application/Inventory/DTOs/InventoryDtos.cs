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
    bool IsActive);

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
    string? RecordedBy);

public sealed record InventoryValuationReportDto(
    Guid FarmId,
    decimal TotalValuationBdt,
    int TotalSkusCount,
    int LowStockCount,
    int OutOfStockCount,
    IReadOnlyList<InventoryItemDto> Items);
