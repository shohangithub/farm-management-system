using Farm360.Domain.Intelligence.Projections;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Intelligence;

#pragma warning disable CA1812
internal sealed class ProjectionScenarioConfiguration : IEntityTypeConfiguration<ProjectionScenario>
{
    public void Configure(EntityTypeBuilder<ProjectionScenario> builder)
    {
        builder.ToTable("ProjectionScenarios");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.StartingLiveWeightKg).HasPrecision(18, 2);
        builder.Property(x => x.PurchasePriceBdt).HasPrecision(18, 2);
        builder.Property(x => x.CurrentMeatPriceBdtPerKg).HasPrecision(18, 2);
        builder.Property(x => x.InitialMeatYieldRatio).HasPrecision(18, 2);
        builder.Property(x => x.DailyLiveWeightGainKg).HasPrecision(18, 2);
        builder.Property(x => x.MeatYieldOnDailyGainRatio).HasPrecision(18, 2);
        builder.Property(x => x.DailyFeedQuantityKgAtStart).HasPrecision(18, 2);
        builder.Property(x => x.FeedPriceBdtPerKg).HasPrecision(18, 2);
        builder.Property(x => x.DailyGrassCostBdt).HasPrecision(18, 2);
        builder.Property(x => x.DailyOtherCostBdt).HasPrecision(18, 2);
        builder.Property(x => x.MonthlyLaborCostBdt).HasPrecision(18, 2);

        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.AnimalId);
    }
}
