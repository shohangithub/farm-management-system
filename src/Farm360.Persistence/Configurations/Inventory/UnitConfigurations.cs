using Farm360.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Inventory;

public sealed class MeasureUnitConfiguration : IEntityTypeConfiguration<MeasureUnit>
{
    public void Configure(EntityTypeBuilder<MeasureUnit> builder)
    {
        builder.ToTable("MeasureUnits", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200);

        builder.HasMany(x => x.Conversions)
            .WithOne()
            .HasForeignKey(c => c.MeasureUnitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Conversions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}

public sealed class UnitConversionConfiguration : IEntityTypeConfiguration<UnitConversion>
{
    public void Configure(EntityTypeBuilder<UnitConversion> builder)
    {
        builder.ToTable("UnitConversions", "inventory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Factor).HasColumnType("decimal(18,6)");
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.HasIndex(x => new { x.MeasureUnitId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
