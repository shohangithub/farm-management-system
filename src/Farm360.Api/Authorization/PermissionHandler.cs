using Farm360.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Farm360.Api.Authorization;

/// <summary>
/// ASP.NET Core authorization handler for permission-based access control.
/// F360-AUTH-2026-001 §7 (Permission-Based Authorization).
///
/// Evaluation order:
///   1. User must be authenticated
///   2. System users (sys=true) bypass all permission checks
///   3. Check JWT embedded permissions (fast, no DB/cache hit)
///   4. Fall back to IPermissionService for cache/DB lookup
///
/// Performance: Most requests resolve in step 3 (JWT claims check).
/// Cache hit: Step 4 with Redis (5ms typical).
/// DB hit: Step 4 cold start only (5-minute cache TTL).
/// </summary>
public sealed class PermissionHandler(
    IPermissionService permissionService,
    ICurrentUserService currentUser,
    ILogger<PermissionHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // 1. Must be authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            logger.LogDebug("Farm360 AuthZ: Unauthenticated request rejected for permission '{Permission}'", requirement.PermissionCode);
            context.Fail();
            return;
        }

        // 2. System users bypass permission checks
        if (currentUser.IsSystemUser)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = currentUser.UserId;
        var tenantId = currentUser.TenantId;

        if (userId is null || tenantId is null)
        {
            context.Fail();
            return;
        }

        // 3. Fast path: check permissions embedded in JWT claims
        var jwtPermissions = context.User.FindFirst("perms")?.Value;
        if (jwtPermissions is not null)
        {
            var permArray = jwtPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (permArray.Contains(requirement.PermissionCode, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // 4. Slow path: check via PermissionService (Redis → DB)
        // Happens when JWT was issued before a role change (or permissions not embedded)
        var hasPermission = await permissionService.HasPermissionAsync(
            userId.Value, tenantId.Value, requirement.PermissionCode);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("Farm360 AuthZ: Permission '{Permission}' denied for User={UserId} Tenant={TenantId}",
                    requirement.PermissionCode, userId, tenantId);
            context.Fail();
        }
    }
}
