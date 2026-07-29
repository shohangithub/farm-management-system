using Farm360.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Inventory;

public sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Sku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Category)
            .IsRequired();

        builder.Property(x => x.UnitOfMeasure)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.ReorderThreshold)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.CurrentStock)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.WeightedAverageCostBdt)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.StorageLocation)
            .HasMaxLength(150);

        builder.HasIndex(x => new { x.TenantId, x.FarmId, x.Sku })
            .IsUnique();
    }
}
