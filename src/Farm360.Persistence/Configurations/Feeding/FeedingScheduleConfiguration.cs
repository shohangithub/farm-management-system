using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedingScheduleConfiguration : IEntityTypeConfiguration<FeedingSchedule>
{
    public void Configure(EntityTypeBuilder<FeedingSchedule> builder)
    {
        builder.ToTable("FeedingSchedules", "feeding");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.TargetQuantityKgPerHead)
            .HasPrecision(18, 2);

        builder.Property(s => s.Frequency)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.HasIndex(s => new { s.TenantId, s.FarmId, s.IsActive });
    }
}
