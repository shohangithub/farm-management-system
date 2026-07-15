using Farm360.Domain.Common;

namespace Farm360.Domain.Identity;

/// <summary>
/// Farm360 custom Role entity (NOT ASP.NET IdentityRole).
/// System roles are seeded and immutable. Tenant admins can create custom roles.
/// F360-AUTH-2026-001 §7.2 (Role-Based Access Control).
/// Constitution §22: TenantId = null means system-wide role. TenantId = Guid means tenant-specific.
/// </summary>
public sealed class Role : BaseEntity
{
    private Role() { }

    private Role(Guid id, Guid? tenantId, string name, string description, bool isSystemRole) : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        IsSystemRole = isSystemRole;
    }

    /// <summary>
    /// Null = system-wide role (seeded, immutable).
    /// Set = tenant-specific custom role.
    /// </summary>
    public Guid? TenantId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// System roles cannot be deleted or modified by tenant admins.
    /// Examples: Owner, FarmManager, Veterinarian, Worker, Viewer.
    /// </summary>
    public bool IsSystemRole { get; private set; }

    public bool IsActive { get; private set; } = true;

    // ── Navigation ───────────────────────────────────────────────────────────
    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    // ── Factory ──────────────────────────────────────────────────────────────
    /// <summary>Creates a seeded system role with a deterministic ID.</summary>
    public static Role CreateSystemRole(Guid id, string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new Role(id, tenantId: null, name.Trim(), description.Trim(), isSystemRole: true);
    }

    /// <summary>Creates a tenant-specific custom role.</summary>
    public static Role CreateTenantRole(Guid tenantId, string name, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required for tenant roles.", nameof(tenantId));

        return new Role(Guid.NewGuid(), tenantId, name.Trim(), description.Trim(), isSystemRole: false);
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void UpdateDetails(string name, string description)
    {
        if (IsSystemRole)
            throw new InvalidOperationException("System roles cannot be modified.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description.Trim();
    }

    public void Deactivate()
    {
        if (IsSystemRole)
            throw new InvalidOperationException("System roles cannot be deactivated.");
        IsActive = false;
    }

    public void AddPermission(Guid permissionId)
    {
        if (_rolePermissions.Any(rp => rp.PermissionId == permissionId)) return;
        _rolePermissions.Add(new RolePermission(Id, permissionId));
    }

    public void RemovePermission(Guid permissionId)
    {
        if (IsSystemRole)
            throw new InvalidOperationException("System role permissions cannot be modified.");
        var rp = _rolePermissions.FirstOrDefault(r => r.PermissionId == permissionId);
        if (rp != null) _rolePermissions.Remove(rp);
    }
}
