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

        builder.Property(vp => vp.AutoStartForPurchased).HasDefaultValue(false);
        builder.Ignore(vp => vp.SpanDays);

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

            stepBuilder.Property(s => s.Kind)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(Farm360.Domain.Health.Enums.HealthTaskKind.Medicine);
            stepBuilder.Property(s => s.DoseAmount).HasPrecision(18, 4);
            stepBuilder.Property(s => s.DosePerKg).HasPrecision(18, 4);
            stepBuilder.Property(s => s.DoseMin).HasPrecision(18, 4);
            stepBuilder.Property(s => s.DoseMax).HasPrecision(18, 4);
            stepBuilder.Property(s => s.DoseRounding)
                .HasConversion<string>()
                .HasMaxLength(10)
                .HasDefaultValue(Farm360.Domain.Health.Enums.DoseRounding.None);
            stepBuilder.Property(s => s.DoseUnit).HasMaxLength(30);
            stepBuilder.Property(s => s.ActionLink).HasMaxLength(50);
            stepBuilder.Ignore(s => s.DoseRule);
        });
    }
}
