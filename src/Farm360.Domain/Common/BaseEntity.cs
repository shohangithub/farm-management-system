namespace Farm360.Domain.Common;

/// <summary>
/// Base entity for all domain entities.
/// Constitution §8 (CQRS + DDD): All entities have a GUID primary key.
/// Constitution §12 (Database Standards): GUID PKs, never int identity.
/// Raises domain events collected here; dispatched after persistence (MediatR).
/// </summary>
public abstract class BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected BaseEntity(Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Entity ID cannot be empty.", nameof(id));

        Id = id;
    }

    /// <summary>EF Core requires a parameterless constructor.</summary>
    protected BaseEntity() { }

    public Guid Id { get; protected set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Remove(domainEvent);
    }

    /// <summary>Called by the DbContext after successful SaveChanges to clear events.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>
/// Marker interface for domain events.
/// Domain events are dispatched in-process via MediatR after commit.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
}
