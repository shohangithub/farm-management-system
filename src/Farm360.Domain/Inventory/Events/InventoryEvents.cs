using Farm360.Domain.Common;

namespace Farm360.Domain.Inventory.Events;

public sealed record StockReceivedEvent(
    Guid TransactionId,
    Guid InventoryItemId,
    Guid TenantId,
    Guid FarmId,
    decimal Quantity,
    decimal UnitCostBdt,
    decimal NewBalance) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record StockDeductedEvent(
    Guid TransactionId,
    Guid InventoryItemId,
    Guid TenantId,
    Guid FarmId,
    decimal Quantity,
    decimal NewBalance) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record LowStockAlertEvent(
    Guid InventoryItemId,
    Guid TenantId,
    Guid FarmId,
    string ItemName,
    decimal CurrentStock,
    decimal ReorderThreshold) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
