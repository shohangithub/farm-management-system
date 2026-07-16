using Farm360.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Farms;

public sealed class FarmConfiguration : IEntityTypeConfiguration<Farm>
{
    public void Configure(EntityTypeBuilder<Farm> builder)
    {
        builder.ToTable("Farms", "app");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.FarmCode).IsRequired().HasMaxLength(50);
        builder.Property(b => b.FarmName).IsRequired().HasMaxLength(200);
        
        builder.Property(b => b.MapPolygon).HasColumnType("nvarchar(max)"); // GeoJSON could be large
        builder.Property(b => b.Description).HasMaxLength(1000);
        builder.Property(b => b.OwnerId).HasMaxLength(36);
        builder.Property(b => b.ManagerId).HasMaxLength(36);

        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("IX_Farms_TenantId");

        builder.HasIndex(b => new { b.TenantId, b.FarmCode })
            .IsUnique()
            .HasDatabaseName("IX_Farms_TenantId_FarmCode");

        builder.HasIndex(b => b.BranchId)
            .HasDatabaseName("IX_Farms_BranchId");
    }
}
