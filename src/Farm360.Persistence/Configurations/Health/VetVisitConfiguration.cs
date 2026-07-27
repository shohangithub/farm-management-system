using Farm360.Domain.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Health;

public sealed class VetVisitConfiguration : IEntityTypeConfiguration<VetVisit>
{
    public void Configure(EntityTypeBuilder<VetVisit> builder)
    {
        builder.ToTable("VetVisits", "app");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.VetName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(v => v.VisitType)
            .IsRequired()
            .HasConversion<int>(); // Maps enum to int

        builder.Property(v => v.Purpose)
            .HasMaxLength(500);

        builder.Property(v => v.Findings)
            .HasMaxLength(2000);

        builder.Property(v => v.Recommendations)
            .HasMaxLength(2000);

        builder.Property(v => v.CostBdt)
            .HasPrecision(10, 2);

        builder.HasIndex(v => new { v.TenantId, v.VisitDate })
            .HasDatabaseName("IX_VetVisits_TenantId_Date");

        builder.HasIndex(v => v.FarmId)
            .HasDatabaseName("IX_VetVisits_FarmId");
    }
}
