using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class FarmShareConfigConfiguration : IEntityTypeConfiguration<FarmShareConfig>
{
    public void Configure(EntityTypeBuilder<FarmShareConfig> builder)
    {
        builder.ToTable("FarmShareConfigs", "finance");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.FarmId).IsRequired();
        builder.Property(c => c.TenantId).IsRequired();

        builder.Property(c => c.TotalShares).IsRequired();
        builder.Property(c => c.SharePriceBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(c => c.OwnerShareCount).IsRequired();
        builder.Property(c => c.AllocatedShareCount).IsRequired();
        builder.Property(c => c.MinimumPurchaseShares).IsRequired();
        builder.Property(c => c.IsShareSaleOpen).IsRequired();
        builder.Property(c => c.ValuationNotes).HasMaxLength(1000);

        builder.HasIndex(c => c.FarmId);
        builder.HasIndex(c => c.TenantId);

        builder.Property(c => c.RowVersion).IsRowVersion();
    }
}

public class ShareHoldingConfiguration : IEntityTypeConfiguration<ShareHolding>
{
    public void Configure(EntityTypeBuilder<ShareHolding> builder)
    {
        builder.ToTable("ShareHoldings", "finance");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.InvestorId).IsRequired();
        builder.Property(h => h.FarmId).IsRequired();
        builder.Property(h => h.TenantId).IsRequired();

        builder.Property(h => h.ShareCount).IsRequired();
        builder.Property(h => h.AveragePurchasePriceBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(h => h.TotalInvestedBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(h => h.CertificateNumber).HasMaxLength(100);
        builder.Property(h => h.Notes).HasMaxLength(1000);
        builder.Property(h => h.IsActive).IsRequired();

        builder.HasIndex(h => new { h.FarmId, h.InvestorId });
        builder.HasIndex(h => h.InvestorId);
        builder.HasIndex(h => h.TenantId);

        builder.Property(h => h.RowVersion).IsRowVersion();
    }
}

public class ShareTransactionConfiguration : IEntityTypeConfiguration<ShareTransaction>
{
    public void Configure(EntityTypeBuilder<ShareTransaction> builder)
    {
        builder.ToTable("ShareTransactions", "finance");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.FarmId).IsRequired();
        builder.Property(t => t.InvestorId).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ShareCount).IsRequired();
        builder.Property(t => t.PricePerShareBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(t => t.TotalAmountBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(t => t.ReferenceId).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasIndex(t => t.FarmId);
        builder.HasIndex(t => t.InvestorId);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.TransactionDate);

        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
