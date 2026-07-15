using Farm360.Domain.Common;

namespace Farm360.Domain.Tenancy;

/// <summary>
/// Organization — a legal entity within a Tenant (e.g. "Northern Division Farm").
/// One Tenant can have multiple Organizations (large cooperatives, corporations).
/// Constitution §22: TenantId is mandatory. Global Query Filter enforces isolation.
/// </summary>
public sealed class Organization : AuditableEntity
{
    private Organization() { }

    private Organization(Guid id, Guid tenantId, string name, OrganizationType type)
        : base(id, tenantId)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; private set; } = string.Empty;
    public OrganizationType Type { get; private set; }
    public string? Description { get; private set; }
    public string? Address { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }

    private readonly List<Branch> _branches = [];
    public IReadOnlyCollection<Branch> Branches => _branches.AsReadOnly();

    // ── Factory ──────────────────────────────────────────────────────────────
    public static Organization Create(Guid tenantId, string name, OrganizationType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return new Organization(Guid.NewGuid(), tenantId, name.Trim(), type);
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void UpdateDetails(string name, string? description, string? address, string? phone, string? email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = description;
        Address = address;
        Phone = phone;
        Email = email;
    }

    public void UpdateType(OrganizationType type) => Type = type;
}
