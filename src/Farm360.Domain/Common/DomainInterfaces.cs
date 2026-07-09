namespace Farm360.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a specific tenant.
/// Constitution §22 (Multi-Tenant Rules): Every business entity carries TenantId.
/// EF Core Global Query Filter enforces tenant isolation automatically.
/// F360-MTA-2026-001: Layer 1 isolation — EF Core Global Query Filter.
/// </summary>
public interface ITenantEntity
{
    /// <summary>
    /// The tenant this entity belongs to.
    /// NEVER expose a query path that bypasses this filter.
    /// Cross-tenant access → 404 (not 403 — do not reveal resource existence).
    /// </summary>
    Guid TenantId { get; }
}

/// <summary>
/// Marks an entity as supporting soft delete.
/// Constitution §12: All business data is soft-deleted only.
/// EF Core Global Query Filter: WHERE IsDeleted = 0 on all queries.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAtUtc { get; }
    Guid? DeletedBy { get; }
}

/// <summary>
/// Marks an entity as an Aggregate Root (DDD).
/// Aggregate roots are the only entities exposed via repositories.
/// Internal entities accessed only through their aggregate root.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyList<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}
