using Farm360.Domain.Inventory.Enums;

namespace Farm360.Application.Inventory.Queries.PurchaseOrders;

public record PurchaseOrderDto(
    Guid Id,
    Guid FarmId,
    string PoNumber,
    Guid SupplierId,
    PurchaseOrderStatus Status,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string? Notes,
    decimal TotalAmountBdt,
    IReadOnlyList<PurchaseOrderItemDto> Items);

public record PurchaseOrderItemDto(
    Guid Id,
    Guid InventoryItemId,
    decimal Quantity,
    decimal UnitCostBdt,
    decimal TotalCostBdt)
{
    /// <summary>The unit it was ordered in ("Bag (50 kg)") and that quantity; Quantity is in the base unit.</summary>
    public string? EnteredUnitName { get; init; }
    public decimal? EnteredQuantity { get; init; }
}
