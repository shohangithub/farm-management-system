using Farm360.Domain.Common;

namespace Farm360.Domain.Inventory.Events;

public sealed record DailyConsumableCostCalculatedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid EntryId,
    Guid TenantId,
    Guid FarmId,
    decimal TotalCostBdt,
    decimal ActualQuantity,
    DateOnly EntryDate,
    Guid InventoryItemId,
    string ItemName) : IDomainEvent;
