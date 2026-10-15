using Farm360.Domain.Finance.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class FarmAccountingSettingsConfiguration : IEntityTypeConfiguration<FarmAccountingSettings>
{
    public void Configure(EntityTypeBuilder<FarmAccountingSettings> builder)
    {
        builder.ToTable("FarmAccountingSettings", "finance");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.FarmId).IsRequired();
        builder.Property(s => s.TenantId).IsRequired();
        builder.Property(s => s.FiscalYearStartMonth).IsRequired();

        // One settings row per farm.
        builder.HasIndex(s => s.FarmId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(s => s.TenantId);

        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}

public class BalanceSheetEntryConfiguration : IEntityTypeConfiguration<BalanceSheetEntry>
{
    public void Configure(EntityTypeBuilder<BalanceSheetEntry> builder)
    {
        builder.ToTable("BalanceSheetEntries", "finance");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FarmId).IsRequired();
        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.Code).HasMaxLength(20).IsRequired();
        builder.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.AccountCode).HasMaxLength(10).IsRequired();
        builder.Property(e => e.ProfitAccountCode).HasMaxLength(10);
        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Notes).HasMaxLength(500);

        builder.HasIndex(e => new { e.FarmId, e.EntryDate });
        builder.HasIndex(e => e.TenantId);

        builder.Property(e => e.RowVersion).IsRowVersion();
    }
}

public class FixedAssetConfiguration : IEntityTypeConfiguration<FixedAsset>
{
    public void Configure(EntityTypeBuilder<FixedAsset> builder)
    {
        builder.ToTable("FixedAssets", "finance");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FarmId).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Code).HasMaxLength(20).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Acquisition).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Method).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(a => a.Cost).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(a => a.RatePercent).HasColumnType("decimal(7,4)").IsRequired();
        builder.Property(a => a.UsefulLifeYears).HasColumnType("decimal(7,2)").IsRequired();
        builder.Property(a => a.ResidualValue).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(a => a.DisposalProceeds).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(a => a.Location).HasMaxLength(200);
        builder.Property(a => a.Notes).HasMaxLength(1000);

        builder.Ignore(a => a.IsDisposed);
        builder.Ignore(a => a.Basis);

        builder.HasIndex(a => a.FarmId);
        builder.HasIndex(a => a.TenantId);

        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
