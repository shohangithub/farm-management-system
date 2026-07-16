using Farm360.Domain.Common;

namespace Farm360.Domain.Farms.Events;

public sealed record FarmCreatedEvent(Guid EventId, DateTime OccurredOnUtc, Farm Farm) : IDomainEvent;
public sealed record FarmUpdatedEvent(Guid EventId, DateTime OccurredOnUtc, Farm Farm) : IDomainEvent;
public sealed record FarmDeletedEvent(Guid EventId, DateTime OccurredOnUtc, Farm Farm) : IDomainEvent;
