using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedConsumptionLogConfiguration : IEntityTypeConfiguration<FeedConsumptionLog>
{
    public void Configure(EntityTypeBuilder<FeedConsumptionLog> builder)
    {
        builder.ToTable("FeedConsumptionLogs", "feeding");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.TotalFeedOfferedKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.TotalRefusalKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.NetConsumptionKg)
            .HasPrecision(18, 2);

        builder.Property(l => l.TotalCostBdt)
            .HasPrecision(18, 2);

        builder.Property(l => l.Notes)
            .HasMaxLength(500);

        builder.HasMany(l => l.Details)
            .WithOne()
            .HasForeignKey(d => d.LogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.TenantId, l.FarmId, l.LogDate });
    }
}

public sealed class ConsumptionDetailConfiguration : IEntityTypeConfiguration<ConsumptionDetail>
{
    public void Configure(EntityTypeBuilder<ConsumptionDetail> builder)
    {
        builder.ToTable("ConsumptionDetails", "feeding");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.OfferedKg)
            .HasPrecision(18, 2);

        builder.Property(d => d.RefusalKg)
            .HasPrecision(18, 2);

        builder.Property(d => d.NetConsumedKg)
            .HasPrecision(18, 2);

        builder.Property(d => d.CostBdt)
            .HasPrecision(18, 2);
    }
}
