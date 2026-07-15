using Farm360.Domain.Health;
using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class MedicalTreatmentConfiguration : IEntityTypeConfiguration<MedicalTreatment>
{
    public void Configure(EntityTypeBuilder<MedicalTreatment> builder)
    {
        builder.ToTable("MedicalTreatments", "app");

        builder.HasKey(mt => mt.Id);

        builder.Property(mt => mt.Diagnosis)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(mt => mt.MedicationName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(mt => mt.VeterinarianName)
            .HasMaxLength(150);

        builder.Property(mt => mt.Notes)
            .HasMaxLength(1000);

        builder.Property(mt => mt.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(mt => mt.CostBdt)
            .HasPrecision(12, 2);

        // Owned Types mapping
        builder.OwnsOne(mt => mt.Dosage, d =>
        {
            d.Property(p => p.Amount)
                .HasColumnName("DosageAmount")
                .HasPrecision(8, 2)
                .IsRequired();
                
            d.Property(p => p.Unit)
                .HasColumnName("DosageUnit")
                .HasMaxLength(20)
                .IsRequired();
        });

        builder.OwnsOne(mt => mt.WithdrawalPeriod, w =>
        {
            w.Property(p => p.MilkDays)
                .HasColumnName("MilkWithdrawalDays")
                .IsRequired();
                
            w.Property(p => p.MeatDays)
                .HasColumnName("MeatWithdrawalDays")
                .IsRequired();
        });

        // Foreign Key Constraints
        builder.HasOne<Animal>()
            .WithMany()
            .HasForeignKey(mt => mt.AnimalId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
