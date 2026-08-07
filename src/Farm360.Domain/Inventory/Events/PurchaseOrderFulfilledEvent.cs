using Farm360.Domain.Common;

namespace Farm360.Domain.Inventory.Events;

public sealed record PurchaseOrderFulfilledEvent(
    Guid PurchaseOrderId,
    Guid TenantId,
    Guid FarmId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
