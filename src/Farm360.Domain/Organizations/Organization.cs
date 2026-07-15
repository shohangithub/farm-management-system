using Farm360.Domain.Common;
using Farm360.Domain.Organizations.Enums;
using Farm360.Domain.Organizations.Events;
using Farm360.Domain.Organizations.ValueObjects;

namespace Farm360.Domain.Organizations;

public sealed class Organization : AuditableEntity, IAggregateRoot
{
    private Organization() { }

    private Organization(
        Guid id,
        Guid tenantId,
        string name,
        string? logoUrl,
        string contactEmail,
        string? contactPhone,
        string? businessRegistrationNumber,
        string? tradeLicenseNumber,
        string? taxIdentificationNumber,
        string currencyCode,
        string timeZoneId,
        string languageCode,
        Address? address,
        BusinessType businessType)
        : base(id, tenantId)
    {
        Name = name;
        LogoUrl = logoUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        BusinessRegistrationNumber = businessRegistrationNumber;
        TradeLicenseNumber = tradeLicenseNumber;
        TaxIdentificationNumber = taxIdentificationNumber;
        CurrencyCode = currencyCode;
        TimeZoneId = timeZoneId;
        LanguageCode = languageCode;
        Address = address;
        BusinessType = businessType;
        Status = OrganizationStatus.Active;
    }

    public string Name { get; private set; } = string.Empty;
    public string? LogoUrl { get; private set; }
    public string ContactEmail { get; private set; } = string.Empty;
    public string? ContactPhone { get; private set; }
    public string? BusinessRegistrationNumber { get; private set; }
    public string? TradeLicenseNumber { get; private set; }
    public string? TaxIdentificationNumber { get; private set; }
    public string CurrencyCode { get; private set; } = "BDT";
    public string TimeZoneId { get; private set; } = "Asia/Dhaka";
    public string LanguageCode { get; private set; } = "en";
    public Address? Address { get; private set; }
    public BusinessType BusinessType { get; private set; }
    public OrganizationStatus Status { get; private set; }

    public static Organization Create(
        Guid tenantId,
        string name,
        string? logoUrl,
        string contactEmail,
        string? contactPhone,
        string? businessRegistrationNumber,
        string? tradeLicenseNumber,
        string? taxIdentificationNumber,
        string currencyCode,
        string timeZoneId,
        string languageCode,
        Address? address,
        BusinessType businessType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty.", nameof(name));
        
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new ArgumentException("Contact email cannot be empty.", nameof(contactEmail));

        var organization = new Organization(
            Guid.NewGuid(),
            tenantId,
            name,
            logoUrl,
            contactEmail,
            contactPhone,
            businessRegistrationNumber,
            tradeLicenseNumber,
            taxIdentificationNumber,
            currencyCode,
            timeZoneId,
            languageCode,
            address,
            businessType);

        organization.RaiseDomainEvent(new OrganizationCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            organization.Id,
            tenantId,
            name));

        return organization;
    }

    public void Update(
        string name,
        string? logoUrl,
        string contactEmail,
        string? contactPhone,
        string? businessRegistrationNumber,
        string? tradeLicenseNumber,
        string? taxIdentificationNumber,
        string currencyCode,
        string timeZoneId,
        string languageCode,
        Address? address,
        BusinessType businessType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name cannot be empty.", nameof(name));
            
        if (string.IsNullOrWhiteSpace(contactEmail))
            throw new ArgumentException("Contact email cannot be empty.", nameof(contactEmail));

        Name = name;
        LogoUrl = logoUrl;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        BusinessRegistrationNumber = businessRegistrationNumber;
        TradeLicenseNumber = tradeLicenseNumber;
        TaxIdentificationNumber = taxIdentificationNumber;
        CurrencyCode = currencyCode;
        TimeZoneId = timeZoneId;
        LanguageCode = languageCode;
        Address = address;
        BusinessType = businessType;

        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            Name));
    }

    public void Deactivate()
    {
        if (Status == OrganizationStatus.Inactive)
            return;

        Status = OrganizationStatus.Inactive;

        RaiseDomainEvent(new OrganizationDeactivatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId));
    }
    
    public void Activate()
    {
        if (Status == OrganizationStatus.Active)
            return;

        Status = OrganizationStatus.Active;
        
        RaiseDomainEvent(new OrganizationUpdatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            Name));
    }
}
