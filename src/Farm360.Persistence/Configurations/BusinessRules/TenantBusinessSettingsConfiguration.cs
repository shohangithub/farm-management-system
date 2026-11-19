using Farm360.Domain.BusinessRules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.BusinessRules;

public class TenantBusinessSettingsConfiguration : IEntityTypeConfiguration<TenantBusinessSettings>
{
    public void Configure(EntityTypeBuilder<TenantBusinessSettings> builder)
    {
        builder.ToTable("TenantBusinessSettings", "app");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.FarmId);

        // Enums stored as strings, like the appsettings section they override.
        builder.Property(s => s.FeedAllocation).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.GroupPlanQuantityBasis).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.OverheadAllocation).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.OverheadCostPeriod).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.Valuation).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.DefaultReportLanguage).HasConversion<string>().HasMaxLength(20);

        builder.Property(s => s.MetabolicWeightExponent);
        builder.Property(s => s.FallbackAnimalWeightKg).HasPrecision(10, 2);
        builder.Property(s => s.DefaultDressingPercentage).HasPrecision(5, 4);
        builder.Property(s => s.DefaultMeatPricePerKgBdt).HasPrecision(18, 2);
        builder.Property(s => s.DefaultLiveWeightPricePerKgBdt).HasPrecision(18, 2);
        builder.Property(s => s.BengaliNumeralsByDefault);

        // One tenant-wide row (FarmId NULL) and at most one row per farm, per tenant.
        // SQL Server unique indexes treat NULLs as equal, so (TenantId, NULL) is unique too.
        builder.HasIndex(s => new { s.TenantId, s.FarmId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("UX_TenantBusinessSettings_TenantId_FarmId");

        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
