using Farm360.Domain.Common;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.ValueObjects;

namespace Farm360.Domain.Organizations;

/// <summary>
/// Branch — a physical location within an Organization (e.g. "Farm Unit A", "Northern Shed").
/// Constitution §22: Inherits TenantId from AuditableEntity.
/// Branch-level access scoping is stored in TenantUser.BranchId (optional).
/// </summary>
public sealed class Branch : AuditableEntity
{
    private Branch() { }

    private Branch(Guid id, Guid tenantId, Guid organizationId, string branchCode, string name, string contactEmail)
        : base(id, tenantId)
    {
        OrganizationId = organizationId;
        BranchCode = branchCode;
        Name = name;
        ContactEmail = contactEmail;
        Status = BranchStatus.Active;
    }

    public Guid OrganizationId { get; private set; }
    
    public string BranchCode { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? ManagerUserId { get; private set; }  // ApplicationUser.Id as string for cross-context ref
    
    public string ContactEmail { get; private set; } = string.Empty;
    public string? ContactPhone { get; private set; }
    
    public Address? Address { get; private set; }
    
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    
    public BranchStatus Status { get; private set; }
    
    public string? WorkingHours { get; private set; } // JSON or string format
    public string? HolidayCalendar { get; private set; } // JSON or string format
    
    public bool IsHeadOffice { get; private set; }

    // ── Navigation (same context) ─────────────────────────────────────────────
    public Organization? Organization { get; private set; }

    // ── Factory ──────────────────────────────────────────────────────────────
    public static Branch Create(
        Guid tenantId, 
        Guid organizationId, 
        string branchCode, 
        string name, 
        string contactEmail,
        bool isHeadOffice = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);
        
        if (organizationId == Guid.Empty) throw new ArgumentException("OrganizationId is required.", nameof(organizationId));

        return new Branch(Guid.NewGuid(), tenantId, organizationId, branchCode.Trim(), name.Trim(), contactEmail.Trim())
        {
            IsHeadOffice = isHeadOffice
        };
    }

    // ── Business methods ─────────────────────────────────────────────────────
    public void UpdateDetails(
        string name, 
        string contactEmail,
        string? contactPhone,
        Address? address,
        double? latitude,
        double? longitude,
        string? workingHours,
        string? holidayCalendar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(contactEmail);

        Name = name.Trim();
        ContactEmail = contactEmail.Trim();
        ContactPhone = contactPhone?.Trim();
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        WorkingHours = workingHours;
        HolidayCalendar = holidayCalendar;
    }

    public void ChangeStatus(BranchStatus status) => Status = status;
    public void SetAsHeadOffice() => IsHeadOffice = true;
    public void UnsetHeadOffice() => IsHeadOffice = false;
    public void AssignManager(Guid userId) => ManagerUserId = userId.ToString();
    public void RemoveManager() => ManagerUserId = null;
}
