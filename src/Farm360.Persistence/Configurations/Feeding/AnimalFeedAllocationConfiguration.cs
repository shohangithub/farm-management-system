using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public class AnimalFeedAllocationConfiguration : IEntityTypeConfiguration<AnimalFeedAllocation>
{
    public void Configure(EntityTypeBuilder<AnimalFeedAllocation> builder)
    {
        builder.ToTable("AnimalFeedAllocations", "feeding");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.AnimalId).IsRequired();
        builder.Property(a => a.FarmId).IsRequired();
        builder.Property(a => a.DailyFeedingEntryId).IsRequired();
        builder.Property(a => a.FeedingPlanId).IsRequired();
        builder.Property(a => a.FormulaId).IsRequired();
        builder.Property(a => a.EntryDate).IsRequired();

        // decimal(18,4): kg to the gram, money to the paisa, with headroom for herd-wide sums.
        builder.Property(a => a.AllocatedKg).HasColumnType("decimal(18,4)");
        builder.Property(a => a.AllocatedCostBdt).HasColumnType("decimal(18,4)");
        builder.Property(a => a.UnitCostBdtPerKg).HasColumnType("decimal(18,4)");
        builder.Property(a => a.WeightAtAllocationKg).HasColumnType("decimal(18,4)");
        builder.Property(a => a.ShareFactor).HasColumnType("decimal(18,6)");

        builder.Property(a => a.Origin).HasConversion<int>();
        builder.Property(a => a.Method).HasConversion<int>();

        // The report access path: one animal over a date range (A1, A3, the cost ledger).
        builder.HasIndex(a => new { a.TenantId, a.AnimalId, a.EntryDate })
            .HasDatabaseName("IX_AnimalFeedAllocations_Tenant_Animal_Date");

        // Herd-wide rollups for a period (H2, H6, reconciliation).
        builder.HasIndex(a => new { a.TenantId, a.FarmId, a.EntryDate })
            .HasDatabaseName("IX_AnimalFeedAllocations_Tenant_Farm_Date");

        // One row per animal per feeding entry. The unique constraint is the last line of defence
        // against a re-run of the daily job double-charging every animal for the same day's feed.
        builder.HasIndex(a => new { a.DailyFeedingEntryId, a.AnimalId })
            .IsUnique()
            .HasDatabaseName("UX_AnimalFeedAllocations_Entry_Animal");

        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
