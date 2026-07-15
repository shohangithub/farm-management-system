using Farm360.Application.Common.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Farm360.Persistence.Permissions;

/// <summary>
/// Permission evaluation service backed by ApplicationDbContext + Redis cache.
/// F360-AUTH-2026-001 §7: Checks TenantUser → Role → RolePermissions → Permission.Code
/// Results cached for 5 minutes. Cache invalidated on role change.
/// Cache key: {tenantId}:permissions:{userId}
///
/// Lives in Farm360.Persistence because it requires EF Core (ApplicationDbContext).
/// Registered via IPermissionService interface (Clean Architecture boundary maintained).
/// </summary>
public sealed class PermissionService(
    ApplicationDbContext context,
    ICacheService cache,
    ILogger<PermissionService> logger)
    : IPermissionService
{
    private const string CacheKeyPrefix = "permissions";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> HasPermissionAsync(
        Guid userId,
        Guid tenantId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var permissions = await GetPermissionsAsync(userId, tenantId, cancellationToken);
        return permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(tenantId, userId);

        var cached = await cache.GetAsync<string[]>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Farm360 Permissions CACHE HIT for User={UserId} Tenant={TenantId}", userId, tenantId);
            return cached;
        }

        // Load from DB: TenantUser -> Role -> RolePermissions -> Permission.Code
        // IgnoreQueryFilters on TenantUsers since we're explicitly filtering by tenantId
        var permissions = await context.TenantUsers
            .AsNoTracking()
            .IgnoreQueryFilters()  // We apply explicit tenant filter below
            .Where(tu => tu.UserId == userId && tu.TenantId == tenantId && !tu.IsDeleted)
            .Join(
                context.RolePermissions.AsNoTracking(),
                tu => tu.RoleId,
                rp => rp.RoleId,
                (tu, rp) => rp.PermissionId)
            .Join(
                context.Permissions.AsNoTracking(),
                permId => permId,
                p => p.Id,
                (permId, p) => p.Code)
            .ToArrayAsync(cancellationToken);

        await cache.SetAsync(cacheKey, permissions, CacheTtl, cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("Farm360 Permissions CACHE MISS: Loaded {Count} permissions for User={UserId}", permissions.Length, userId);

        return permissions;
    }

    public async Task InvalidatePermissionCacheAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(tenantId, userId);
        await cache.RemoveAsync(cacheKey, cancellationToken);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Farm360 Permissions cache invalidated for User={UserId} Tenant={TenantId}", userId, tenantId);
    }

    private static string BuildCacheKey(Guid tenantId, Guid userId)
        => $"{tenantId}:{CacheKeyPrefix}:{userId}";
}
