namespace Farm360.Contracts.Envelopes;

/// <summary>
/// Outbox message — stores integration events as DB rows before publishing.
/// Constitution §8 (CQRS): Domain events → Integration events via Transactional Outbox pattern.
/// Pattern: write outbox row in SAME transaction as business data, then publish asynchronously.
/// F360-MTA-2026-001: TenantId is mandatory on all outbox messages.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Fully qualified CLR type name of the integration event.</summary>
    public string EventType { get; init; } = string.Empty;

    /// <summary>JSON-serialized integration event payload.</summary>
    public string Payload { get; init; } = string.Empty;

    /// <summary>Tenant this event belongs to. Mandatory — no anonymous events.</summary>
    public Guid TenantId { get; init; }

    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;

    /// <summary>Null = not yet processed. Set when successfully published.</summary>
    public DateTime? ProcessedOnUtc { get; set; }

    /// <summary>Error message from last failed publish attempt.</summary>
    public string? Error { get; set; }

    /// <summary>Number of failed publish attempts. Circuit-breaker after 5.</summary>
    public int RetryCount { get; set; }
}
