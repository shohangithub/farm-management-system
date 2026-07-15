using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

/// <summary>
/// EF Core Fluent API configuration for BreedingRecord (child of Animal aggregate).
/// Constitution §3.1: Fluent API only.
/// </summary>
public sealed class BreedingRecordConfiguration : IEntityTypeConfiguration<BreedingRecord>
{
    public void Configure(EntityTypeBuilder<BreedingRecord> builder)
    {
        builder.ToTable("BreedingRecords", "app");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.AnimalId).IsRequired();

        builder.Property(b => b.MatingDate).IsRequired();

        // Optional FK to on-platform sire — set OnDelete to NoAction to avoid multiple cascade paths
        builder.Property(b => b.SireAnimalId).IsRequired(false);

        builder.Property(b => b.SireExternalId)
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(b => b.IsArtificialInsemination)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(b => b.PregnancyConfirmDate).IsRequired(false);
        builder.Property(b => b.IsPregnancyConfirmed).IsRequired().HasDefaultValue(false);
        builder.Property(b => b.ExpectedCalvingDate).IsRequired(false);
        builder.Property(b => b.ActualCalvingDate).IsRequired(false);

        builder.Property(b => b.CalvingOutcome)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(b => b.CalvesCount).IsRequired(false);
        builder.Property(b => b.RecordedBy).IsRequired();
        builder.Property(b => b.CreatedAtUtc).IsRequired();

        builder.HasIndex(b => b.AnimalId)
            .HasDatabaseName("IX_BreedingRecords_AnimalId");

        // Query: "show pregnancies expected to calve soon"
        builder.HasIndex(b => new { b.IsPregnancyConfirmed, b.ExpectedCalvingDate })
            .HasDatabaseName("IX_BreedingRecords_PregnancyConfirmed_ExpectedCalving");
    }
}
