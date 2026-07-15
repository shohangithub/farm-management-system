using Farm360.Domain.Organizations.Enums;

namespace Farm360.Application.Organizations.DTOs;

public record OrganizationDto(
    Guid Id,
    string Name,
    string? LogoUrl,
    string ContactEmail,
    string? ContactPhone,
    string? BusinessRegistrationNumber,
    string? TradeLicenseNumber,
    string? TaxIdentificationNumber,
    string CurrencyCode,
    string TimeZoneId,
    string LanguageCode,
    AddressDto? Address,
    BusinessType BusinessType,
    OrganizationStatus Status,
    byte[] RowVersion);

public record AddressDto(
    string Street,
    string City,
    string State,
    string Country,
    string ZipCode);
