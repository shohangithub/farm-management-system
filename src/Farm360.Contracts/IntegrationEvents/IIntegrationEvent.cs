namespace Farm360.Contracts.IntegrationEvents;

/// <summary>
/// Marker interface for all integration events.
/// Used for outbox pattern — events published after successful commit.
/// F360-CONST-2026-001 §8 (CQRS): Domain events → Integration events via Outbox.
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
    string EventType { get; }
    Guid TenantId { get; }
}

/// <summary>Base record for all integration events.</summary>
public abstract record BaseIntegrationEvent(Guid TenantId) : IIntegrationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
}
