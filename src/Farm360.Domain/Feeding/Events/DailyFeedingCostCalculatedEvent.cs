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
    Guid? ShedId,
    decimal StockCostBdt = 0m) : IDomainEvent
{
    /// <summary>
    /// The part of <see cref="TotalCostBdt"/> that came out of inventory (ingredients linked to a
    /// stock item and actually deducted). That part was paid for when the stock was bought; the rest
    /// (ingredients not tracked in stock) is a direct cash cost on the day it is fed.
    /// </summary>
    public decimal DirectCostBdt => Math.Max(0m, TotalCostBdt - StockCostBdt);
}
