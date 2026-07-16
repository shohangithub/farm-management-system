using Farm360.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Organizations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", "app");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.BranchCode).IsRequired().HasMaxLength(50);
        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.ContactEmail).IsRequired().HasMaxLength(150);
        builder.Property(b => b.ContactPhone).HasMaxLength(30);
        builder.Property(b => b.ManagerUserId).HasMaxLength(36); // GUID string
        builder.Property(b => b.WorkingHours).HasMaxLength(1000);
        builder.Property(b => b.HolidayCalendar).HasMaxLength(2000);

        builder.OwnsOne(b => b.Address, a =>
        {
            a.Property(p => p.Street).HasMaxLength(200).HasColumnName("Address_Street");
            a.Property(p => p.City).HasMaxLength(100).HasColumnName("Address_City");
            a.Property(p => p.State).HasMaxLength(100).HasColumnName("Address_State");
            a.Property(p => p.Country).HasMaxLength(100).HasColumnName("Address_Country");
            a.Property(p => p.ZipCode).HasMaxLength(20).HasColumnName("Address_ZipCode");
        });

        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("IX_Branches_TenantId");

        builder.HasIndex(b => new { b.TenantId, b.BranchCode })
            .IsUnique()
            .HasDatabaseName("IX_Branches_TenantId_BranchCode");

        builder.HasIndex(b => b.OrganizationId)
            .HasDatabaseName("IX_Branches_OrganizationId");

        builder.HasIndex(b => new { b.TenantId, b.IsHeadOffice })
            .HasDatabaseName("IX_Branches_TenantId_HeadOffice");

        builder.Property(b => b.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
