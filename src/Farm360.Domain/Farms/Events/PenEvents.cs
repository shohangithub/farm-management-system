using Farm360.Domain.Common;

namespace Farm360.Domain.Farms.Events;

public sealed record PenCreatedEvent(Guid EventId, DateTime OccurredOnUtc, Pen Pen) : IDomainEvent;
public sealed record PenUpdatedEvent(Guid EventId, DateTime OccurredOnUtc, Pen Pen) : IDomainEvent;
public sealed record PenDeletedEvent(Guid EventId, DateTime OccurredOnUtc, Pen Pen) : IDomainEvent;
