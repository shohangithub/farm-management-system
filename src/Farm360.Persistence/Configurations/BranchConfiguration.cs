using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("Branches", "app");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).IsRequired().HasMaxLength(200);
        builder.Property(b => b.Location).HasMaxLength(500);
        builder.Property(b => b.GpsCoordinates).HasMaxLength(50);
        builder.Property(b => b.ManagerUserId).HasMaxLength(36); // GUID string

        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("IX_Branches_TenantId");

        builder.HasIndex(b => b.OrganizationId)
            .HasDatabaseName("IX_Branches_OrganizationId");

        builder.HasIndex(b => new { b.TenantId, b.IsHeadOffice })
            .HasDatabaseName("IX_Branches_TenantId_HeadOffice");

        builder.Property(b => b.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
