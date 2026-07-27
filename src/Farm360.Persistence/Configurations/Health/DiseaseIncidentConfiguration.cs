using Farm360.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class DiseaseIncidentConfiguration : IEntityTypeConfiguration<DiseaseIncident>
{
    public void Configure(EntityTypeBuilder<DiseaseIncident> builder)
    {
        builder.ToTable("DiseaseIncidents", "app");

        builder.HasKey(di => di.Id);

        builder.Property(di => di.DiseaseName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(di => di.Symptoms)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(di => di.Notes)
            .HasMaxLength(1000);

        builder.Property(di => di.Severity)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(di => di.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        // FarmId and ShedId are just Guid references; no navigation properties in this module per Clean Architecture
        // Assuming Farm and Shed might be in different bounded contexts or just stored as plain IDs

        builder.PrimitiveCollection(di => di.AffectedAnimalIds)
            .HasColumnName("AffectedAnimalIds");
    }
}
