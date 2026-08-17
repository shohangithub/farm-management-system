using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Events;

public sealed record DailyEntryConfirmedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid EntryId,
    Guid TenantId,
    Guid FarmId,
    decimal ActualKg,
    Guid? InventoryTransactionId) : IDomainEvent;
