using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Events;

public sealed record ReconciliationApprovedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid ReconciliationId,
    Guid TenantId,
    Guid FarmId) : IDomainEvent;
