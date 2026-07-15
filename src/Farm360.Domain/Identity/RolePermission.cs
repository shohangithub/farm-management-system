namespace Farm360.Domain.Identity;

/// <summary>
/// Join entity linking a Role to a Permission.
/// EF Core: Composite PK (RoleId + PermissionId). No surrogate key needed.
/// System role permissions are seeded — protected by IsSystemRole check on Role.
/// </summary>
public sealed class RolePermission
{
    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty) throw new ArgumentException("RoleId is required.", nameof(roleId));
        if (permissionId == Guid.Empty) throw new ArgumentException("PermissionId is required.", nameof(permissionId));
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    // ── Navigation ───────────────────────────────────────────────────────────
    public Role? Role { get; private set; }
    public Permission? Permission { get; private set; }
}
