using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Events;

public sealed record DailyFeedingCostCalculatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid EntryId,
    Guid TenantId,
    Guid FarmId,
    decimal TotalCostBdt,
    decimal ActualKg,
    DateOnly EntryDate,
    Guid? AnimalId,
    Guid? BatchId,
    Guid? ShedId) : IDomainEvent;
