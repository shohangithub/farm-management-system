using Farm360.Domain.Organizations;

namespace Farm360.Application.Organizations.DTOs;

public static class MappingExtensions
{
    public static OrganizationDto ToDto(this Organization entity)
    {
        return new OrganizationDto(
            entity.Id,
            entity.Name,
            entity.LogoUrl,
            entity.ContactEmail,
            entity.ContactPhone,
            entity.BusinessRegistrationNumber,
            entity.TradeLicenseNumber,
            entity.TaxIdentificationNumber,
            entity.CurrencyCode,
            entity.TimeZoneId,
            entity.LanguageCode,
            entity.Address != null ? new AddressDto(
                entity.Address.Street,
                entity.Address.City,
                entity.Address.State,
                entity.Address.Country,
                entity.Address.ZipCode) : null,
            entity.BusinessType,
            entity.Status,
            entity.CreatedAtUtc,
            entity.CreatedBy.ToString(),
            entity.ModifiedAtUtc,
            entity.ModifiedBy?.ToString(),
            entity.RowVersion);
    }
}
