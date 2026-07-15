using Farm360.Domain.Common;
using Farm360.Domain.Organizations;

namespace Farm360.Domain.Tenancy;

/// <summary>
/// Branch — a physical location within an Organization (e.g. "Farm Unit A", "Northern Shed").
/// Constitution §22: Inherits TenantId from AuditableEntity.
/// Branch-level access scoping is stored in TenantUser.BranchId (optional).
/// </summary>
public sealed class Branch : AuditableEntity
{
    private Branch() { }

    private Branch(Guid id, Guid tenantId, Guid organizationId, string name)
        : base(id, tenantId)
    {
        OrganizationId = organizationId;
        Name = name;
    }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string? GpsCoordinates { get; private set; }  // "lat,lng"
    public bool IsHeadOffice { get; private set; }
    public string? ManagerUserId { get; private set; }  // ApplicationUser.Id as string for cross-context ref

    // ── Navigation (same context) ─────────────────────────────────────────────
    public Organization? Organization { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────
    public static Branch Create(Guid tenantId, Guid organizationId, string name, bool isHeadOffice = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (organizationId == Guid.Empty) throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        return new Branch(Guid.NewGuid(), tenantId, organizationId, name.Trim())
        {
            IsHeadOffice = isHeadOffice
        };
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void UpdateDetails(string name, string? location, string? gpsCoordinates)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Location = location;
        GpsCoordinates = gpsCoordinates;
    }

    public void SetAsHeadOffice() => IsHeadOffice = true;
    public void UnsetHeadOffice() => IsHeadOffice = false;
    public void AssignManager(Guid userId) => ManagerUserId = userId.ToString();
}
