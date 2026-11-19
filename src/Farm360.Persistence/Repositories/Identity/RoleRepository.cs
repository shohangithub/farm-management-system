using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Identity;

public sealed class RoleRepository(ApplicationDbContext context) : IRoleRepository
{
    public Task<Role?> GetAssignableAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken = default) =>
        context.Roles.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.IsActive
                                      && (r.IsSystemRole || r.TenantId == tenantId), cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAssignableAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await context.Roles.IgnoreQueryFilters().AsNoTracking()
            .Where(r => r.IsActive && (r.IsSystemRole || r.TenantId == tenantId))
            .OrderByDescending(r => r.IsSystemRole)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        await context.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Join(context.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync(cancellationToken);
}
