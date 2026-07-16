using Farm360.Domain.Common;

namespace Farm360.Domain.MasterData.Events;

public sealed record MasterDataEntryCreatedEvent(Guid EventId, DateTime OccurredOnUtc, MasterDataEntry Entry) : IDomainEvent;
public sealed record MasterDataEntryUpdatedEvent(Guid EventId, DateTime OccurredOnUtc, MasterDataEntry Entry) : IDomainEvent;
public sealed record MasterDataEntryDeletedEvent(Guid EventId, DateTime OccurredOnUtc, MasterDataEntry Entry) : IDomainEvent;
