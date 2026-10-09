using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Inventory.DTOs;

public sealed record PurchaseReturnDto(
    Guid Id,
    Guid FarmId,
    string ReturnNumber,
    Guid PurchaseOrderId,
    string? PoNumber,
    Guid SupplierId,
    string? SupplierName,
    DateOnly ReturnDate,
    PurchaseReturnStatus Status,
    PurchaseReturnReason Reason,
    string? Notes,
    string? CreditNoteNumber,
    decimal TotalAmountBdt,
    IReadOnlyList<PurchaseReturnItemDto> Items);

public sealed record PurchaseReturnItemDto(
    Guid Id,
    Guid PurchaseOrderItemId,
    Guid InventoryItemId,
    string? ItemName,
    string? UnitOfMeasure,
    decimal Quantity,
    decimal UnitCostBdt,
    decimal TotalCostBdt);

public sealed record ReturnablePoItemDto(
    Guid PurchaseOrderItemId,
    Guid InventoryItemId,
    string ItemName,
    string UnitOfMeasure,
    decimal OrderedQuantity,
    decimal AlreadyReturnedQuantity,
    decimal CurrentStock,
    decimal MaxReturnableQuantity,
    decimal UnitCostBdt);

public sealed record CreatePurchaseReturnItemRequest(
    Guid PurchaseOrderItemId,
    decimal Quantity);
