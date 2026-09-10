using Farm360.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Inventory;

public sealed class DailyConsumableEntryConfiguration : IEntityTypeConfiguration<DailyConsumableEntry>
{
    public void Configure(EntityTypeBuilder<DailyConsumableEntry> builder)
    {
        builder.ToTable("DailyConsumableEntries", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpectedQuantity)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.ActualQuantity)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.UnitCostAtConsumptionBdt)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.TotalCostBdt)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.AdjustmentReason)
            .HasMaxLength(300);

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ConsumableUsagePlan>()
            .WithMany()
            .HasForeignKey(x => x.ConsumableUsagePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.FarmId, x.EntryDate });
        builder.HasIndex(x => new { x.ConsumableUsagePlanId, x.ConsumableUsagePlanItemId, x.EntryDate });
    }
}
