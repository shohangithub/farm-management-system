using Farm360.Domain.Common;

namespace Farm360.Domain.Organizations.Events;

public sealed record OrganizationDeactivatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid OrganizationId,
    Guid TenantId) : IDomainEvent;
