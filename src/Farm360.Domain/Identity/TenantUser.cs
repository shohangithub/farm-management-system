using Farm360.Domain.Common;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Organizations;

namespace Farm360.Domain.Identity;

/// <summary>
/// TenantUser — links an ApplicationUser to a Tenant with a specific Role.
/// One user can belong to multiple tenants (e.g. a vet serving multiple farms).
/// F360-MTA-2026-001 §3 (Tenant User Model): Per-tenant role assignment.
/// Constitution §22: TenantId enforced via AuditableEntity Global Query Filter.
/// </summary>
public sealed class TenantUser : AuditableEntity
{
    private TenantUser() { }

    private TenantUser(Guid id, Guid tenantId, Guid userId, Guid roleId) : base(id, tenantId)
    {
        UserId = userId;
        RoleId = roleId;
    }

    /// <summary>FK to identity.Users (ApplicationUser). Cross-context reference by ID.</summary>
    public Guid UserId { get; private set; }

    /// <summary>FK to app.Roles — determines permissions within this tenant.</summary>
    public Guid RoleId { get; private set; }

    /// <summary>Optional: restrict user to a specific branch. Null = all branches.</summary>
    public Guid? BranchId { get; private set; }

    /// <summary>The tenant owner. Cannot be demoted or removed (only replaced by Owner transfer).</summary>
    public bool IsOwner { get; private set; }

    /// <summary>When the invitation was sent (for pending invitations).</summary>
    public DateTime? InvitedAt { get; private set; }

    /// <summary>When the user accepted the invitation.</summary>
    public DateTime? JoinedAt { get; private set; }

    /// <summary>Status: Pending, Active, Deactivated.</summary>
    public TenantUserStatus Status { get; private set; } = TenantUserStatus.Pending;

    // ── Navigation (same DbContext) ───────────────────────────────────────────
    public Role? Role { get; private set; }
    public Branch? Branch { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────
    public static TenantUser Invite(Guid tenantId, Guid userId, Guid roleId, Guid? branchId = null)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
        if (roleId == Guid.Empty) throw new ArgumentException("RoleId is required.", nameof(roleId));

        return new TenantUser(Guid.NewGuid(), tenantId, userId, roleId)
        {
            BranchId = branchId,
            InvitedAt = DateTime.UtcNow,
            Status = TenantUserStatus.Pending
        };
    }

    /// <summary>Creates an already-active tenant user (used for tenant owner on creation).</summary>
    public static TenantUser CreateOwner(Guid tenantId, Guid userId, Guid ownerRoleId)
    {
        return new TenantUser(Guid.NewGuid(), tenantId, userId, ownerRoleId)
        {
            IsOwner = true,
            JoinedAt = DateTime.UtcNow,
            Status = TenantUserStatus.Active
        };
    }

    /// <summary>
    /// Creates an active membership for a user who accepted an invitation.
    /// A member joining with the Owner role is a co-owner.
    /// </summary>
    public static TenantUser Join(Guid tenantId, Guid userId, Guid roleId, Guid? branchId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
        if (roleId == Guid.Empty) throw new ArgumentException("RoleId is required.", nameof(roleId));

        var now = DateTime.UtcNow;
        return new TenantUser(Guid.NewGuid(), tenantId, userId, roleId)
        {
            BranchId = branchId,
            IsOwner = roleId == SystemRoleIds.Owner,
            InvitedAt = now,
            JoinedAt = now,
            Status = TenantUserStatus.Active
        };
    }

    /// <summary>True for the tenant creator and for any member holding the Owner role.</summary>
    public bool IsOwnerMember => IsOwner || RoleId == SystemRoleIds.Owner;

    // ── Business methods ─────────────────────────────────────────────────────
    public void Accept()
    {
        if (Status != TenantUserStatus.Pending)
            throw new InvalidOperationException("Only pending invitations can be accepted.");
        Status = TenantUserStatus.Active;
        JoinedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Changes the member's role. An owner can only be demoted while another active owner remains
    /// (<paramref name="anotherActiveOwnerExists"/>), so a tenant never loses its last owner.
    /// </summary>
    public void ChangeRole(Guid newRoleId, bool anotherActiveOwnerExists = false)
    {
        if (newRoleId == Guid.Empty) throw new ArgumentException("RoleId is required.", nameof(newRoleId));

        var demotingOwner = IsOwnerMember && newRoleId != SystemRoleIds.Owner;
        if (demotingOwner && !anotherActiveOwnerExists)
            throw new InvalidOperationException("The last owner cannot be demoted. Make another member an owner first.");

        RoleId = newRoleId;
        IsOwner = newRoleId == SystemRoleIds.Owner;
    }

    public void AssignBranch(Guid? branchId) => BranchId = branchId;

    /// <summary>Deactivates the membership. The last active owner cannot be deactivated.</summary>
    public void Deactivate(bool anotherActiveOwnerExists = false)
    {
        if (IsOwnerMember && !anotherActiveOwnerExists)
            throw new InvalidOperationException("Cannot deactivate the last tenant owner.");
        if (Status == TenantUserStatus.Deactivated)
            throw new InvalidOperationException("This member is already deactivated.");
        Status = TenantUserStatus.Deactivated;
    }

    public void Reactivate()
    {
        if (Status != TenantUserStatus.Deactivated)
            throw new InvalidOperationException("Only deactivated members can be reactivated.");
        Status = TenantUserStatus.Active;
    }

    /// <summary>Removes the member from the tenant (soft delete). The last active owner cannot be removed.</summary>
    public void Remove(Guid removedBy, bool anotherActiveOwnerExists = false)
    {
        if (IsOwnerMember && !anotherActiveOwnerExists)
            throw new InvalidOperationException("Cannot remove the last tenant owner.");
        SoftDelete(removedBy);
    }
}

/// <summary>Lifecycle state of a TenantUser's membership.</summary>
public enum TenantUserStatus
{
    Pending = 1,
    Active = 2,
    Deactivated = 3
}
