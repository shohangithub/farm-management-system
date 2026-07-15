using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

/// <summary>
/// EF Core Fluent API configuration for WeightRecord (child of Animal aggregate).
/// Constitution §3.1: Fluent API only — no data annotations on domain entities.
/// </summary>
public sealed class WeightRecordConfiguration : IEntityTypeConfiguration<WeightRecord>
{
    public void Configure(EntityTypeBuilder<WeightRecord> builder)
    {
        builder.ToTable("WeightRecords", "app");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.AnimalId).IsRequired();

        // Owned Value Object: Weight → WeightKg column
        builder.OwnsOne(w => w.Weight, weightBuilder =>
        {
            weightBuilder.Property(wt => wt.WeightKg)
                .HasColumnName("WeightKg")
                .HasPrecision(8, 2)
                .IsRequired();
        });

        builder.Property(w => w.RecordedDate).IsRequired();
        builder.Property(w => w.RecordedBy).IsRequired();

        builder.Property(w => w.Notes)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(w => w.RecordedAtUtc).IsRequired();

        // FK to Animal is defined in AnimalConfiguration via HasMany/WithOne
        builder.HasIndex(w => w.AnimalId)
            .HasDatabaseName("IX_WeightRecords_AnimalId");

        builder.HasIndex(w => new { w.AnimalId, w.RecordedDate })
            .HasDatabaseName("IX_WeightRecords_AnimalId_RecordedDate");
    }
}
