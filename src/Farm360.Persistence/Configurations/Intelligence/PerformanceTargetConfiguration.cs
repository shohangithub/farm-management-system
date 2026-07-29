using Farm360.Domain.Intelligence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Intelligence;

public class PerformanceTargetConfiguration : IEntityTypeConfiguration<PerformanceTarget>
{
    public void Configure(EntityTypeBuilder<PerformanceTarget> builder)
    {
        builder.ToTable("PerformanceTargets", "intelligence");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.BreedName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Stage)
            .IsRequired()
            .HasMaxLength(50);
            
        builder.Property(x => x.TargetAdgKg)
            .HasColumnType("decimal(18,2)");
            
        builder.Property(x => x.TargetFcr)
            .HasColumnType("decimal(18,2)");
            
        builder.Property(x => x.TargetCostPerKgGainBdt)
            .HasColumnType("decimal(18,2)");

        builder.HasIndex(x => new { x.BreedName, x.Stage });
    }
}
