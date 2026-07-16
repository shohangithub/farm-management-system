using Farm360.Domain.Common;

namespace Farm360.Domain.Organizations.Events;

public sealed record BranchCreatedEvent(Guid EventId, DateTime OccurredOnUtc, Branch Branch) : IDomainEvent;
public sealed record BranchUpdatedEvent(Guid EventId, DateTime OccurredOnUtc, Branch Branch) : IDomainEvent;
public sealed record BranchDeletedEvent(Guid EventId, DateTime OccurredOnUtc, Branch Branch) : IDomainEvent;
