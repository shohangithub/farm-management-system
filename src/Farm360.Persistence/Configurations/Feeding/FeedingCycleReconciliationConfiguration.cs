using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedingCycleReconciliationConfiguration : IEntityTypeConfiguration<FeedingCycleReconciliation>
{
    public void Configure(EntityTypeBuilder<FeedingCycleReconciliation> builder)
    {
        builder.ToTable("FeedingCycleReconciliations", "feeding");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TotalExpectedKg)
            .HasPrecision(18, 2);

        builder.Property(r => r.TotalActualKg)
            .HasPrecision(18, 2);

        builder.Ignore(r => r.VarianceKg);

        builder.Property(r => r.Status)
            .IsRequired();

        builder.HasMany(r => r.Lines)
            .WithOne()
            .HasForeignKey(l => l.FeedingCycleReconciliationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.TenantId, r.FarmId, r.PeriodStart, r.PeriodEnd });
    }
}

public sealed class FeedingCycleReconciliationLineConfiguration : IEntityTypeConfiguration<FeedingCycleReconciliationLine>
{
    public void Configure(EntityTypeBuilder<FeedingCycleReconciliationLine> builder)
    {
        builder.ToTable("FeedingCycleReconciliationLines", "feeding");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.ExpectedQty)
            .HasPrecision(18, 2);

        builder.Property(l => l.ActualQty)
            .HasPrecision(18, 2);

        builder.Ignore(l => l.VarianceQty);
    }
}
