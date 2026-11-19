using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Identity;

/// <summary>
/// Membership queries take the tenant explicitly (IgnoreQueryFilters + TenantId predicate): some
/// callers run without an ambient tenant (anonymous invitation accept), and the explicit predicate
/// keeps every query scoped to one tenant.
/// </summary>
public sealed class TenantUserRepository : ITenantUserRepository
{
    private readonly ApplicationDbContext _context;

    public TenantUserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TenantUser entity, CancellationToken cancellationToken = default)
    {
        await _context.TenantUsers.AddAsync(entity, cancellationToken);
    }

    public async Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TenantUsers.FindAsync([id], cancellationToken);
    }

    public Task<bool> UserHasAnyMembershipAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        // Deliberately cross-tenant: the caller has no tenant yet, and a removed membership
        // still counts (a user gets one self-service trial tenant).
        return _context.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(tu => tu.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<TenantUser>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await _context.TenantUsers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(tu => tu.Role)
            .Include(tu => tu.Branch)
            .Where(tu => tu.TenantId == tenantId && !tu.IsDeleted)
            .OrderBy(tu => tu.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<TenantUser?> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default)
    {
        return _context.TenantUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tu => tu.Id == memberId && tu.TenantId == tenantId && !tu.IsDeleted, cancellationToken);
    }

    public Task<TenantUser?> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        return _context.TenantUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(tu => tu.UserId == userId && tu.TenantId == tenantId && !tu.IsDeleted, cancellationToken);
    }

    public Task<int> CountSeatHoldersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return _context.TenantUsers
            .IgnoreQueryFilters()
            .CountAsync(tu => tu.TenantId == tenantId && !tu.IsDeleted
                              && (tu.Status == TenantUserStatus.Active || tu.Status == TenantUserStatus.Pending),
                cancellationToken);
    }

    public Task<bool> AnotherActiveOwnerExistsAsync(Guid tenantId, Guid excludingMemberId, CancellationToken cancellationToken = default)
    {
        return _context.TenantUsers
            .IgnoreQueryFilters()
            .AnyAsync(tu => tu.TenantId == tenantId && !tu.IsDeleted && tu.Id != excludingMemberId
                            && tu.Status == TenantUserStatus.Active
                            && (tu.IsOwner || tu.RoleId == SystemRoleIds.Owner),
                cancellationToken);
    }
}
