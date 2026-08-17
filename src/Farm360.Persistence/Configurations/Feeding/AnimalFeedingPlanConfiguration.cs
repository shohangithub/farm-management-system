using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class AnimalFeedingPlanConfiguration : IEntityTypeConfiguration<AnimalFeedingPlan>
{
    public void Configure(EntityTypeBuilder<AnimalFeedingPlan> builder)
    {
        builder.ToTable("AnimalFeedingPlans", "feeding");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PlanType)
            .IsRequired();

        builder.Property(p => p.Status)
            .IsRequired();

        builder.Property(p => p.CurrentConcentrateKgPerDay)
            .HasPrecision(18, 2);

        builder.Property(p => p.CurrentRoughageKgPerDay)
            .HasPrecision(18, 2);

        builder.Property(p => p.TriggeredByWeightKg)
            .HasPrecision(18, 2);

        builder.HasMany(p => p.Exclusions)
            .WithOne()
            .HasForeignKey(e => e.AnimalFeedingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.TenantId, p.FarmId, p.Status });
        builder.HasIndex(p => p.AnimalId);
        builder.HasIndex(p => p.BatchId);
        builder.HasIndex(p => p.PenId);
        builder.HasIndex(p => p.ShedId);
    }
}

public sealed class FeedingPlanExclusionConfiguration : IEntityTypeConfiguration<FeedingPlanExclusion>
{
    public void Configure(EntityTypeBuilder<FeedingPlanExclusion> builder)
    {
        builder.ToTable("FeedingPlanExclusions", "feeding");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Reason)
            .IsRequired()
            .HasMaxLength(250);
    }
}
