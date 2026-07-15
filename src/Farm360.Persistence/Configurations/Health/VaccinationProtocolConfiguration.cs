using Farm360.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class VaccinationProtocolConfiguration : IEntityTypeConfiguration<VaccinationProtocol>
{
    public void Configure(EntityTypeBuilder<VaccinationProtocol> builder)
    {
        builder.ToTable("VaccinationProtocols", "app");

        builder.HasKey(vp => vp.Id);

        builder.Property(vp => vp.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(vp => vp.Description)
            .HasMaxLength(1000);

        builder.Property(vp => vp.TargetSpecies)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.OwnsMany(vp => vp.Steps, stepBuilder =>
        {
            stepBuilder.ToTable("VaccinationProtocolSteps", "app");
            
            stepBuilder.HasKey(s => s.Id);
            stepBuilder.WithOwner().HasForeignKey(s => s.ProtocolId);

            stepBuilder.Property(s => s.StepName)
                .IsRequired()
                .HasMaxLength(100);

            stepBuilder.Property(s => s.VaccineName)
                .IsRequired()
                .HasMaxLength(100);

            stepBuilder.Property(s => s.DosageInstruction)
                .HasMaxLength(250);
        });
    }
}
