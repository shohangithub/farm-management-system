using Farm360.Domain.Farms;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Farms;

public sealed class ShedConfiguration : IEntityTypeConfiguration<Shed>
{
    public void Configure(EntityTypeBuilder<Shed> builder)
    {
        builder.ToTable("Sheds", "app");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.ShedNumber).IsRequired().HasMaxLength(50);
        builder.Property(b => b.ShedName).IsRequired().HasMaxLength(200);
        builder.Property(b => b.AnimalType).HasMaxLength(100);
        builder.Property(b => b.FloorType).HasMaxLength(100);
        builder.Property(b => b.RoofType).HasMaxLength(100);

        builder.HasIndex(b => b.TenantId)
            .HasDatabaseName("IX_Sheds_TenantId");

        // Unique constraint on TenantId + FarmId + ShedNumber
        builder.HasIndex(b => new { b.TenantId, b.FarmId, b.ShedNumber })
            .IsUnique()
            .HasDatabaseName("IX_Sheds_Tenant_Farm_ShedNumber");
    }
}
