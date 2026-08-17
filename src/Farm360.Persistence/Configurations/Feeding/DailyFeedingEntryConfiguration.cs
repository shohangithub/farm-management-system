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
            .HasPrecision(18, 2);

        builder.Property(e => e.ActualKg)
            .HasPrecision(18, 2);

        builder.Property(e => e.AdjustmentReason)
            .HasMaxLength(250);

        builder.Property(e => e.Status)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.FarmId, e.EntryDate });
        builder.HasIndex(e => e.FeedingPlanId);
        builder.HasIndex(e => e.Status);
    }
}
