using Farm360.Domain.Common;

namespace Farm360.Domain.Inventory.Events;

public sealed record StockWriteOffEvent(
    Guid TransactionId,
    Guid ItemId,
    Guid TenantId,
    Guid FarmId,
    decimal WriteOffQuantity,
    string Reason,
    decimal UnitCostBdt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
