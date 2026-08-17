using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedingRuleSetConfiguration : IEntityTypeConfiguration<FeedingRuleSet>
{
    public void Configure(EntityTypeBuilder<FeedingRuleSet> builder)
    {
        builder.ToTable("FeedingRuleSets", "feeding");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(r => r.Species)
            .IsRequired();

        builder.Property(r => r.Purpose)
            .IsRequired();

        builder.HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.FeedingRuleSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(r => r.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(r => new { r.TenantId, r.Species, r.Purpose });
    }
}

public sealed class FeedingRuleLineConfiguration : IEntityTypeConfiguration<FeedingRuleLine>
{
    public void Configure(EntityTypeBuilder<FeedingRuleLine> builder)
    {
        builder.ToTable("FeedingRuleLines", "feeding");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.WeightFromKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.WeightToKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.MinWeightKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.MaxWeightKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.QuantityValue)
            .HasPrecision(18, 2);

        builder.Property(l => l.ConcentrateKgPerDay)
            .HasPrecision(18, 2);

        builder.Property(l => l.RoughageKgPerDay)
            .HasPrecision(18, 2);

        builder.Property(l => l.ProteinTargetPercent)
            .HasPrecision(5, 2);
    }
}
