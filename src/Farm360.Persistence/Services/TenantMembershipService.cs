using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Identity;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Services;

/// <summary>
/// Persistence-layer implementation of ITenantMembershipService.
/// Resolves a user's active TenantUser membership to determine the correct
/// TenantId and Role to embed in the JWT at login / token refresh.
///
/// EF global query filters (TenantId, IsDeleted) are bypassed here via
/// IgnoreQueryFilters() because we are querying ACROSS tenants to find which
/// tenant this user belongs to. The WHERE UserId == ... clause ensures we only
/// see THIS user's own memberships — no cross-tenant data leak.
///
/// Registered in DI as IScoped (per-request lifetime).
/// </summary>
public sealed class TenantMembershipService(ApplicationDbContext dbContext) : ITenantMembershipService
{
    public async Task<TenantMembership?> GetActiveMembershipAsync(
        Guid userId,
        Guid? preferredTenantId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.TenantUsers
            .IgnoreQueryFilters()
            .Include(tu => tu.Role)
            .Where(tu =>
                tu.UserId == userId &&
                tu.Status == TenantUserStatus.Active &&
                !tu.IsDeleted);

        // If the caller specifies a preferred tenant (e.g., the tenant from an existing session),
        // prefer that membership. Otherwise fall back to the earliest active one.
        if (preferredTenantId.HasValue && preferredTenantId.Value != Guid.Empty)
        {
            query = query.Where(tu => tu.TenantId == preferredTenantId.Value);
        }
        else
        {
            query = query.OrderBy(tu => tu.JoinedAt);
        }

        var membership = await query.FirstOrDefaultAsync(cancellationToken);

        if (membership == null)
            return null;

        var roleName = membership.Role?.Name ?? "Viewer";
        return new TenantMembership(membership.TenantId, roleName);
    }
}
