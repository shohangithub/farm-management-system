namespace Farm360.Domain.Tenancy;

/// <summary>
/// Business Audit Log — immutable record of all entity changes within a tenant.
/// Constitution §11 (Logging): INSERT-ONLY. Never UPDATE or DELETE.
/// F360-MTA-2026-001: TenantId is mandatory. NOT inheriting AuditableEntity (that would be circular).
/// Populated by AuditSaveChangesInterceptor automatically.
/// </summary>
public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(
        Guid tenantId,
        string entityName,
        Guid entityId,
        string action,
        string? oldValues,
        string? newValues,
        Guid? changedBy,
        string? correlationId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        OldValues = oldValues;
        NewValues = newValues;
        ChangedBy = changedBy;
        CorrelationId = correlationId;
        OccurredAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; init; }

    /// <summary>Tenant this audit entry belongs to.</summary>
    public Guid TenantId { get; init; }

    /// <summary>CLR type name of the audited entity (e.g. "Organization").</summary>
    public string EntityName { get; init; } = string.Empty;

    /// <summary>Primary key of the audited entity.</summary>
    public Guid EntityId { get; init; }

    /// <summary>One of: Created | Updated | Deleted | Restored.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>JSON snapshot of property values before change. Null on Create.</summary>
    public string? OldValues { get; init; }

    /// <summary>JSON snapshot of property values after change. Null on Delete.</summary>
    public string? NewValues { get; init; }

    /// <summary>ApplicationUser.Id of the user who made the change.</summary>
    public Guid? ChangedBy { get; init; }

    /// <summary>HTTP request correlation ID for tracing.</summary>
    public string? CorrelationId { get; init; }

    public DateTime OccurredAtUtc { get; init; }
}
