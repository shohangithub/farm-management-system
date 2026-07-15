using Farm360.Domain.Common;

namespace Farm360.Domain.Organizations.Events;

public sealed record OrganizationCreatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid OrganizationId,
    Guid TenantId,
    string Name) : IDomainEvent;
