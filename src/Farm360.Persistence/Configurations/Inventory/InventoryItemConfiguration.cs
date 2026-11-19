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

        // Optimistic concurrency on stock: two simultaneous deductions from the same item must not
        // both read the same balance and overwrite each other. The loser gets a
        // DbUpdateConcurrencyException (HTTP 409, "reload and try again").
        builder.Property(x => x.RowVersion).IsRowVersion();

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
            .HasMaxLength(40);

        // Item → base unit (stock is counted in it) and → its usual purchase/issue unit, as in
        // FrostTrack: Product.DefaultUnitId → UnitConversion → BaseUnit.
        builder.HasOne<MeasureUnit>()
            .WithMany()
            .HasForeignKey(x => x.BaseUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<UnitConversion>()
            .WithMany()
            .HasForeignKey(x => x.DefaultUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ReorderThreshold)
            .HasColumnType("decimal(18,2)");

        // 4 decimals: stock kept in kg must still hold a few grams used for one animal.
        builder.Property(x => x.CurrentStock)
            .HasColumnType("decimal(18,4)");

        // 6 decimals in the base unit: a gram of premix can cost 0.0021 BDT (InventoryItem.CostDecimals).
        builder.Property(x => x.WeightedAverageCostBdt)
            .HasColumnType("decimal(18,6)");

        builder.Property(x => x.StorageLocation)
            .HasMaxLength(150);

        builder.HasIndex(x => new { x.TenantId, x.FarmId, x.Sku })
            .IsUnique();
    }
}
