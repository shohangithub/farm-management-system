using Farm360.Domain.Common;

namespace Farm360.Domain.Inventory.Events;

public sealed record PurchaseReturnCompletedEvent(
    Guid PurchaseReturnId,
    Guid TenantId,
    Guid FarmId,
    Guid PurchaseOrderId,
    Guid SupplierId,
    string ReturnNumber,
    decimal TotalAmountBdt,
    DateOnly ReturnDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
