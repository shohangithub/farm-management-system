using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Farm360.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor — populates audit columns automatically.
/// Constitution §12: CreatedBy/CreatedAt, ModifiedBy/ModifiedAt auto-populated.
/// Constitution §22 (Multi-Tenant): Cross-tenant write guard (Layer 2).
/// F360-MTA-2026-001 Golden Rule §3: AuditSaveChangesInterceptor is the last line of defence.
///
/// Layer 2 protection: If ApplicationDbContext GlobalQueryFilter (Layer 1) somehow
/// fails (e.g., developer called .IgnoreQueryFilters()), this interceptor STILL prevents
/// any cross-tenant write from reaching the database.
/// </summary>
public sealed class AuditSaveChangesInterceptor(
    ICurrentUserService currentUser,
    IDateTimeService dateTime,
    ILogger<AuditSaveChangesInterceptor> logger)
    : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var now = dateTime.UtcNow;
        var userId = currentUser.UserId ?? Guid.Empty;
        var tenantId = currentUser.TenantId;

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // ── Layer 2: Cross-tenant write guard ──────────────────
                    if (tenantId.HasValue && entry.Entity.TenantId != Guid.Empty
                        && entry.Entity.TenantId != tenantId.Value
                        && !currentUser.IsSystemUser)
                    {
                        var entityName = entry.Entity.GetType().Name;
                        if (logger.IsEnabled(LogLevel.Critical))
                        {
                            logger.LogCritical(
                                "CROSS-TENANT WRITE BLOCKED: Attempt to write {EntityName} belonging to tenant {EntityTenantId} by user {UserId} in tenant {CurrentTenantId}",
                                entityName, entry.Entity.TenantId, userId, tenantId.Value);
                        }

                        throw new CrossTenantWriteException(
                            $"Cross-tenant write attempt blocked: {entityName}. Entity tenant: {entry.Entity.TenantId}, Current tenant: {tenantId.Value}");
                    }

                    entry.Entity.SetCreatedAudit(userId, now);
                    break;

                case EntityState.Modified:
                    // ── Layer 2: Cross-tenant modification guard ───────────
                    if (tenantId.HasValue && entry.Entity.TenantId != tenantId.Value
                        && !currentUser.IsSystemUser)
                    {
                        var entityName = entry.Entity.GetType().Name;
                        if (logger.IsEnabled(LogLevel.Critical))
                        {
                            logger.LogCritical(
                                "CROSS-TENANT MODIFY BLOCKED: {EntityName} TenantId={EntityTenantId} by user {UserId} in tenant {CurrentTenantId}",
                                entityName, entry.Entity.TenantId, userId, tenantId.Value);
                        }

                        throw new CrossTenantWriteException(
                            $"Cross-tenant modification attempt blocked: {entityName}");
                    }

                    entry.Entity.SetModifiedAudit(userId, now);
                    break;

                case EntityState.Deleted:
                    // All deletes must be soft-deletes — hard deletes are forbidden.
                    // Constitution §12: Business data is NEVER hard-deleted.
                    // If you reach here, the developer forgot to call SoftDelete() first.
                    var deletedEntityName = entry.Entity.GetType().Name;
                    if (logger.IsEnabled(LogLevel.Error))
                    {
                        logger.LogError(
                            "HARD DELETE INTERCEPTED: {EntityName} Id={EntityId} — converting to soft delete.",
                            deletedEntityName, entry.Entity.Id);
                    }

                    // Convert hard delete to soft delete
                    entry.State = EntityState.Modified;
                    entry.Entity.SoftDelete(userId);
                    break;
            }
        }
    }
}

/// <summary>
/// Cross-tenant write attempt detected — fatal security violation.
/// F360-MTA-2026-001 Golden Rule §3.
/// </summary>
public sealed class CrossTenantWriteException(string message) : InvalidOperationException(message);
