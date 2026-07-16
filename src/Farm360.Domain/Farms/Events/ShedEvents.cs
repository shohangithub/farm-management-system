using Farm360.Domain.Common;

namespace Farm360.Domain.Farms.Events;

public sealed record ShedCreatedEvent(Guid EventId, DateTime OccurredOnUtc, Shed Shed) : IDomainEvent;
public sealed record ShedUpdatedEvent(Guid EventId, DateTime OccurredOnUtc, Shed Shed) : IDomainEvent;
public sealed record ShedDeletedEvent(Guid EventId, DateTime OccurredOnUtc, Shed Shed) : IDomainEvent;
