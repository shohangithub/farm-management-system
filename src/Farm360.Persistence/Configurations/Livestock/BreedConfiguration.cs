using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

public sealed class BreedConfiguration : IEntityTypeConfiguration<Breed>
{
    public void Configure(EntityTypeBuilder<Breed> builder)
    {
        builder.ToTable("Breeds");

        builder.HasKey(b => b.Id);

        // Required multi-tenant filter
        builder.HasQueryFilter(b => !b.IsDeleted);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.Description)
            .HasMaxLength(1000);

        builder.Property(b => b.Category)
            .HasMaxLength(50);

        builder.Property(b => b.Origin)
            .HasMaxLength(100);

        builder.Property(b => b.MainPurpose)
            .HasMaxLength(50);

        builder.Property(b => b.BestFor)
            .HasMaxLength(200);

        // Growth Metrics
        builder.Property(b => b.AdgPoorManagement).HasColumnType("decimal(18,2)");
        builder.Property(b => b.AdgAverageFarm).HasColumnType("decimal(18,2)");
        builder.Property(b => b.AdgGoodCommercialFarm).HasColumnType("decimal(18,2)");
        builder.Property(b => b.AdgIntensiveFattening).HasColumnType("decimal(18,2)");
        
        builder.Property(b => b.StandardAdgMin).HasColumnType("decimal(18,2)");
        builder.Property(b => b.StandardAdgMax).HasColumnType("decimal(18,2)");

        // Efficiency
        builder.Property(b => b.FcrMin).HasColumnType("decimal(18,2)");
        builder.Property(b => b.FcrMax).HasColumnType("decimal(18,2)");

        // Dairy Metrics
        builder.Property(b => b.MilkYieldMinLiters).HasColumnType("decimal(18,2)");
        builder.Property(b => b.MilkYieldMaxLiters).HasColumnType("decimal(18,2)");
        builder.Property(b => b.FatPercentageMin).HasColumnType("decimal(18,2)");
        builder.Property(b => b.FatPercentageMax).HasColumnType("decimal(18,2)");
    }
}
