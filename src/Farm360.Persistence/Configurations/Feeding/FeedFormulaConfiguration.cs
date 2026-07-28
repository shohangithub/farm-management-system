using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedFormulaConfiguration : IEntityTypeConfiguration<FeedFormula>
{
    public void Configure(EntityTypeBuilder<FeedFormula> builder)
    {
        builder.ToTable("FeedFormulas", "feeding");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(f => f.TargetSpecies)
            .IsRequired();

        builder.Property(f => f.TargetStage)
            .HasMaxLength(100);

        builder.Property(f => f.Status)
            .IsRequired();

        builder.Property(f => f.TotalCostPerKgBdt)
            .HasPrecision(18, 2);

        builder.Property(f => f.Description)
            .HasMaxLength(1000);

        builder.OwnsOne(f => f.NutritionalProfile, np =>
        {
            np.Property(p => p.DryMatterPercentage).HasColumnName("DryMatterPct").HasPrecision(5, 2);
            np.Property(p => p.CrudeProteinPercentage).HasColumnName("CrudeProteinPct").HasPrecision(5, 2);
            np.Property(p => p.MetabolizableEnergyMjPerKg).HasColumnName("MetabolizableEnergyMjPerKg").HasPrecision(8, 4);
            np.Property(p => p.CrudeFiberPercentage).HasColumnName("CrudeFiberPct").HasPrecision(5, 2);
            np.Property(p => p.CalciumPercentage).HasColumnName("CalciumPct").HasPrecision(5, 2);
            np.Property(p => p.PhosphorusPercentage).HasColumnName("PhosphorusPct").HasPrecision(5, 2);
        });

        builder.HasMany(f => f.Ingredients)
            .WithOne()
            .HasForeignKey(i => i.FormulaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.TenantId, f.Title });
    }
}

public sealed class FormulaIngredientConfiguration : IEntityTypeConfiguration<FormulaIngredient>
{
    public void Configure(EntityTypeBuilder<FormulaIngredient> builder)
    {
        builder.ToTable("FormulaIngredients", "feeding");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Percentage)
            .HasPrecision(5, 2);

        builder.Property(i => i.IngredientCostPerKg)
            .HasPrecision(18, 2);

        builder.OwnsOne(i => i.IngredientNutritionalProfile, np =>
        {
            np.Property(p => p.DryMatterPercentage).HasColumnName("DryMatterPct").HasPrecision(5, 2);
            np.Property(p => p.CrudeProteinPercentage).HasColumnName("CrudeProteinPct").HasPrecision(5, 2);
            np.Property(p => p.MetabolizableEnergyMjPerKg).HasColumnName("MetabolizableEnergyMjPerKg").HasPrecision(8, 4);
            np.Property(p => p.CrudeFiberPercentage).HasColumnName("CrudeFiberPct").HasPrecision(5, 2);
            np.Property(p => p.CalciumPercentage).HasColumnName("CalciumPct").HasPrecision(5, 2);
            np.Property(p => p.PhosphorusPercentage).HasColumnName("PhosphorusPct").HasPrecision(5, 2);
        });
    }
}
