using System;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Domain.Identity.Repositories;

public interface ITenantUserRepository
{
    Task AddAsync(TenantUser entity, CancellationToken cancellationToken = default);
    Task<TenantUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the user has, or ever had, a membership in any tenant (across all tenants,
    /// including removed memberships). Used to allow only one self-service trial tenant per user.
    /// </summary>
    Task<bool> UserHasAnyMembershipAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Non-removed members of the tenant with Role and Branch loaded (untracked).</summary>
    Task<IReadOnlyList<TenantUser>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, non-removed membership of the tenant by membership id.</summary>
    Task<TenantUser?> GetMemberAsync(Guid tenantId, Guid memberId, CancellationToken cancellationToken = default);

    /// <summary>Tracked, non-removed membership of a user in the tenant (explicit tenant, no ambient filter).</summary>
    Task<TenantUser?> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Members occupying a seat: Active + Pending, not removed (explicit tenant).</summary>
    Task<int> CountSeatHoldersAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>True when an active owner other than <paramref name="excludingMemberId"/> exists.</summary>
    Task<bool> AnotherActiveOwnerExistsAsync(Guid tenantId, Guid excludingMemberId, CancellationToken cancellationToken = default);
}
