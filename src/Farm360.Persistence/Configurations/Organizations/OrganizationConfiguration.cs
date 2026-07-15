using Farm360.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Organizations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations", "app");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(o => o.LogoUrl)
            .HasMaxLength(500);

        builder.Property(o => o.ContactEmail)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(o => o.ContactPhone)
            .HasMaxLength(30);

        builder.Property(o => o.BusinessRegistrationNumber)
            .HasMaxLength(100);

        builder.Property(o => o.TradeLicenseNumber)
            .HasMaxLength(100);

        builder.Property(o => o.TaxIdentificationNumber)
            .HasMaxLength(100);

        builder.Property(o => o.CurrencyCode)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(o => o.TimeZoneId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(o => o.LanguageCode)
            .IsRequired()
            .HasMaxLength(10);

        builder.OwnsOne(o => o.Address, a =>
        {
            a.Property(p => p.Street).HasColumnName("AddressStreet").HasMaxLength(200);
            a.Property(p => p.City).HasColumnName("AddressCity").HasMaxLength(100);
            a.Property(p => p.State).HasColumnName("AddressState").HasMaxLength(100);
            a.Property(p => p.Country).HasColumnName("AddressCountry").HasMaxLength(100);
            a.Property(p => p.ZipCode).HasColumnName("AddressZipCode").HasMaxLength(20);
        });

        builder.Property(o => o.BusinessType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(o => o.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Name is unique per Tenant
        builder.HasIndex(o => new { o.TenantId, o.Name })
            .IsUnique();

        // Multi-tenant index
        builder.HasIndex(o => o.TenantId);

        builder.Property(o => o.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
