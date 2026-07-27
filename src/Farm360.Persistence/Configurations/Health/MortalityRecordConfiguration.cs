using Farm360.Domain.Health;
using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class MortalityRecordConfiguration : IEntityTypeConfiguration<MortalityRecord>
{
    public void Configure(EntityTypeBuilder<MortalityRecord> builder)
    {
        builder.ToTable("MortalityRecords", "app");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.DiseaseName)
            .HasMaxLength(200);

        builder.Property(m => m.PostMortemNotes)
            .HasMaxLength(2000);

        builder.Property(m => m.CauseOfDeath)
            .IsRequired()
            .HasConversion<int>(); // Maps enum to int

        builder.Property(m => m.EstimatedEconomicLossBdt)
            .HasPrecision(18, 4);

        // One death per animal
        builder.HasIndex(m => m.AnimalId)
            .IsUnique()
            .HasDatabaseName("UQ_Mortality_AnimalId");

        builder.HasIndex(m => new { m.TenantId, m.DeathDate })
            .HasDatabaseName("IX_Mortality_TenantId_Date");

        // Foreign Key Constraints
        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(m => m.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DiseaseIncident>()
            .WithMany()
            .HasForeignKey(m => m.DiseaseIncidentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
