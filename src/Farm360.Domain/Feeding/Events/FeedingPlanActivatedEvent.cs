using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Events;

public sealed record FeedingPlanActivatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid PlanId,
    Guid TenantId,
    Guid FarmId) : IDomainEvent;
