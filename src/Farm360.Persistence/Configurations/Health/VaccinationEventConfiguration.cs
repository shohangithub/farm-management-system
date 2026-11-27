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

        builder.Property(ve => ve.Kind)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Farm360.Domain.Health.Enums.HealthTaskKind.Medicine);
        builder.Property(ve => ve.StepName).HasMaxLength(100);
        builder.Property(ve => ve.Instruction).HasMaxLength(250);
        builder.Property(ve => ve.DoseAmount).HasPrecision(18, 4);
        builder.Property(ve => ve.DosePerKg).HasPrecision(18, 4);
        builder.Property(ve => ve.DoseMin).HasPrecision(18, 4);
        builder.Property(ve => ve.DoseMax).HasPrecision(18, 4);
        builder.Property(ve => ve.DoseRounding)
            .HasConversion<string>()
            .HasMaxLength(10)
            .HasDefaultValue(Farm360.Domain.Health.Enums.DoseRounding.None);
        builder.Property(ve => ve.DoseUnit).HasMaxLength(30);
        builder.Property(ve => ve.ActionLink).HasMaxLength(50);
        builder.Property(ve => ve.DoseWeightKg).HasPrecision(10, 2);
        builder.Property(ve => ve.SkipReason).HasMaxLength(500);

        builder.Ignore(ve => ve.DoseRule);
        builder.Ignore(ve => ve.IsRecurring);
        builder.Ignore(ve => ve.IsPending);
        builder.Ignore(ve => ve.IsCareTask);
        builder.Ignore(ve => ve.ClosedOn);

        // Today's tasks / reminders: pending work by date; the next dose of a chain by its parent.
        builder.HasIndex(ve => new { ve.TenantId, ve.Status, ve.ScheduledDate })
            .HasDatabaseName("IX_VaccinationEvents_Tenant_Status_Date");
        builder.HasIndex(ve => ve.PreviousEventId)
            .HasDatabaseName("IX_VaccinationEvents_PreviousEventId");

        // Foreign Key Constraints
        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(ve => ve.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProtocolStepId is stored as a loose reference (Guid?) without a strict FK 
        // because VaccinationProtocolStep is an owned entity type.
    }
}
