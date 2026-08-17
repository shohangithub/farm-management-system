using Farm360.Domain.Feeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Feeding;

public sealed class FeedIngredientConfiguration : IEntityTypeConfiguration<FeedIngredient>
{
    public void Configure(EntityTypeBuilder<FeedIngredient> builder)
    {
        builder.ToTable("FeedIngredients", "feeding");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(i => i.Category)
            .IsRequired();

        builder.Property(i => i.Unit)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("kg");

        builder.Property(i => i.UnitCostBdt)
            .HasPrecision(18, 2);

        builder.Property(i => i.Description)
            .HasMaxLength(500);

        builder.OwnsOne(i => i.NutritionalProfile, np =>
        {
            np.Property(p => p.DryMatterPercentage).HasColumnName("DryMatterPct").HasPrecision(5, 2);
            np.Property(p => p.CrudeProteinPercentage).HasColumnName("CrudeProteinPct").HasPrecision(5, 2);
            np.Property(p => p.MetabolizableEnergyMjPerKg).HasColumnName("MetabolizableEnergyMjPerKg").HasPrecision(8, 4);
            np.Property(p => p.CrudeFiberPercentage).HasColumnName("CrudeFiberPct").HasPrecision(5, 2);
            np.Property(p => p.CalciumPercentage).HasColumnName("CalciumPct").HasPrecision(5, 2);
            np.Property(p => p.PhosphorusPercentage).HasColumnName("PhosphorusPct").HasPrecision(5, 2);
        });

        builder.HasIndex(i => new { i.TenantId, i.Name });
        builder.HasIndex(i => i.InventoryItemId);
    }
}
