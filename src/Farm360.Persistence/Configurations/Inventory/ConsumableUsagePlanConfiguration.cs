using Farm360.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Inventory;

public sealed class ConsumableUsagePlanConfiguration : IEntityTypeConfiguration<ConsumableUsagePlan>
{
    public void Configure(EntityTypeBuilder<ConsumableUsagePlan> builder)
    {
        builder.ToTable("ConsumableUsagePlans", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.StartDate)
            .IsRequired();

        // Configure private collection navigation
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.ConsumableUsagePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TenantId, x.FarmId, x.Status });
    }
}

public sealed class ConsumableUsagePlanItemConfiguration : IEntityTypeConfiguration<ConsumableUsagePlanItem>
{
    public void Configure(EntityTypeBuilder<ConsumableUsagePlanItem> builder)
    {
        builder.ToTable("ConsumableUsagePlanItems", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PlannedQuantityPerDay)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(250);

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(x => x.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ConsumableUsagePlanId, x.InventoryItemId });
    }
}
