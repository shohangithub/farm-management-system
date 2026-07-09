namespace Farm360.Domain.Common;

/// <summary>
/// Auditable entity — all business entities extend this.
/// Constitution §12 (Database Standards): Every table has Created/Modified audit columns.
/// Constitution §22 (Multi-Tenant Rules): Every business entity carries TenantId.
/// Soft delete: IsDeleted + DeletedAt — never hard-delete business data.
/// </summary>
public abstract class AuditableEntity : BaseEntity, ITenantEntity, ISoftDeletable
{
    protected AuditableEntity(Guid id, Guid tenantId) : base(id)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Required by EF Core.</summary>
    protected AuditableEntity() { }

    // ── Audit columns (Constitution §12) ────────────────────────────────────
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    // ── Soft delete (Constitution §12: no hard deletes in business data) ────
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    // ── Multi-tenant (Constitution §22) ─────────────────────────────────────
    public Guid TenantId { get; private set; }

    // ── EF Concurrency token ─────────────────────────────────────────────────
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Called by AuditSaveChangesInterceptor on entity creation.
    /// Never call manually from application code.
    /// </summary>
    protected internal void SetCreatedAudit(Guid createdBy, DateTime createdAtUtc)
    {
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>
    /// Called by AuditSaveChangesInterceptor on entity modification.
    /// Never call manually from application code.
    /// </summary>
    protected internal void SetModifiedAudit(Guid modifiedBy, DateTime modifiedAtUtc)
    {
        ModifiedBy = modifiedBy;
        ModifiedAtUtc = modifiedAtUtc;
    }

    /// <summary>
    /// Soft-deletes this entity.
    /// Constitution §12: Business data is NEVER hard-deleted.
    /// </summary>
    public void SoftDelete(Guid deletedBy)
    {
        IsDeleted = true;
        DeletedAtUtc = DateTime.UtcNow;
        DeletedBy = deletedBy;
    }
}
