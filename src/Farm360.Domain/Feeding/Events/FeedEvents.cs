using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Events;

public sealed record FeedConsumptionLoggedEvent(
    Guid LogId,
    Guid TenantId,
    Guid FarmId,
    Guid? ShedId,
    Guid? PenId,
    Guid FormulaId,
    DateOnly LogDate,
    decimal NetConsumptionKg,
    decimal TotalCostBdt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record FeedFormulaCreatedEvent(
    Guid FormulaId,
    Guid TenantId,
    string Title,
    decimal TotalCostPerKgBdt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record FeedingScheduleCreatedEvent(
    Guid ScheduleId,
    Guid TenantId,
    Guid FarmId,
    Guid FormulaId,
    DateOnly StartDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
