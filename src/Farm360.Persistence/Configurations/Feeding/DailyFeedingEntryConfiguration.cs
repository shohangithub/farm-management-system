using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class DailyFeedingEntryConfiguration : IEntityTypeConfiguration<DailyFeedingEntry>
{
    public void Configure(EntityTypeBuilder<DailyFeedingEntry> builder)
    {
        builder.ToTable("DailyFeedingEntries", "feeding");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ExpectedKg)
            .HasPrecision(18, 4);

        builder.Property(e => e.ActualKg)
            .HasPrecision(18, 4);

        builder.Property(e => e.UnitCostAtConsumptionBdt)
            .HasPrecision(18, 4);

        builder.Property(e => e.TotalCostBdt)
            .HasPrecision(18, 2);

        builder.Property(e => e.AdjustmentReason)
            .HasMaxLength(250);

        builder.Property(e => e.Status)
            .IsRequired();

        // Optimistic concurrency: two people confirming the same entry at once — the second save
        // fails (409) instead of deducting stock twice.
        builder.Property(e => e.RowVersion).IsRowVersion();

        builder.HasIndex(e => new { e.TenantId, e.FarmId, e.EntryDate });
        builder.HasIndex(e => e.FeedingPlanId);
        builder.HasIndex(e => e.Status);

        // The daily job's idempotency key: one entry per plan, rule line and day. Entries without a
        // rule line (legacy rows) are outside the key; soft-deleted rows do not block a re-run.
        builder.HasIndex(e => new { e.FeedingPlanId, e.RuleLineId, e.EntryDate })
            .IsUnique()
            .HasFilter("[RuleLineId] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_DailyFeedingEntries_Plan_RuleLine_Date");
    }
}
