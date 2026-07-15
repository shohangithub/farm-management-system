using Farm360.Domain.Common;

namespace Farm360.Domain.Identity;

/// <summary>
/// Permission — a single granular action a user can be authorized to perform.
/// Code format: "{module}.{action}" e.g. "animals.view", "reports.export"
/// Permissions are SEEDED by the system — tenant admins cannot create new permission codes.
/// F360-AUTH-2026-001 §7 (Permission-Based Authorization).
/// </summary>
public sealed class Permission : BaseEntity
{
    private Permission() { }

    private Permission(Guid id, string code, string module, string description) : base(id)
    {
        Code = code;
        Module = module;
        Description = description;
    }

    /// <summary>
    /// Unique permission code (lowercase, dot-separated).
    /// Pattern: "{module}.{action}" — e.g. "animals.view", "health.delete"
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Logical grouping module name (e.g. "Animals", "Health", "Reports").</summary>
    public string Module { get; private set; } = string.Empty;

    /// <summary>Human-readable description shown in UI permission management.</summary>
    public string Description { get; private set; } = string.Empty;

    // ── Navigation ───────────────────────────────────────────────────────────
    private readonly List<RolePermission> _rolePermissions = [];
    public IReadOnlyCollection<RolePermission> RolePermissions => _rolePermissions.AsReadOnly();

    // ── Factory ──────────────────────────────────────────────────────────────
    public static Permission Create(string code, string module, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(module);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!code.Contains('.'))
            throw new ArgumentException("Permission code must follow '{module}.{action}' format.", nameof(code));

        return new Permission(Guid.NewGuid(), code.ToLowerInvariant().Trim(), module.Trim(), description.Trim());
    }

    /// <summary>Used by seeder to recreate with a known deterministic GUID.</summary>
    public static Permission Seed(Guid id, string code, string module, string description)
        => new(id, code.ToLowerInvariant(), module, description);
}
