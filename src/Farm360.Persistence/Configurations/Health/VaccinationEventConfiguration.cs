using Farm360.Domain.Health;
using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class VaccinationEventConfiguration : IEntityTypeConfiguration<VaccinationEvent>
{
    public void Configure(EntityTypeBuilder<VaccinationEvent> builder)
    {
        builder.ToTable("VaccinationEvents", "app");

        builder.HasKey(ve => ve.Id);

        builder.Property(ve => ve.VaccineName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(ve => ve.BatchNumber)
            .HasMaxLength(50);

        builder.Property(ve => ve.Notes)
            .HasMaxLength(1000);

        builder.Property(ve => ve.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        // Foreign Key Constraints
        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(ve => ve.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProtocolStepId is stored as a loose reference (Guid?) without a strict FK 
        // because VaccinationProtocolStep is an owned entity type.
    }
}
