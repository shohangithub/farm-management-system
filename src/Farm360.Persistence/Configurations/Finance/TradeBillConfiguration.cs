using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class TradeBillConfiguration : IEntityTypeConfiguration<TradeBill>
{
    public void Configure(EntityTypeBuilder<TradeBill> builder)
    {
        builder.ToTable("TradeBills", "finance");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.FarmId).IsRequired();
        builder.Property(b => b.TenantId).IsRequired();

        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(b => b.Category).HasConversion<string>().HasMaxLength(50).IsRequired();

        builder.Property(b => b.PartyName).HasMaxLength(200).IsRequired();
        builder.Property(b => b.PartyPhone).HasMaxLength(50);
        builder.Property(b => b.SourceModule).HasMaxLength(50);
        builder.Property(b => b.ReferenceNo).HasMaxLength(100).IsRequired();
        builder.Property(b => b.Description).HasMaxLength(500).IsRequired();
        builder.Property(b => b.Notes).HasMaxLength(1000);

        builder.Property(b => b.AmountBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(b => b.PaidBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(b => b.AdjustedBdt).HasColumnType("decimal(18,2)").IsRequired();

        builder.Ignore(b => b.OutstandingBdt);
        builder.Ignore(b => b.Status);
        builder.Ignore(b => b.IsOpen);
        builder.Ignore(b => b.HasSettlements);
        builder.Ignore(b => b.Settlement);

        builder.HasIndex(b => b.TenantId);
        builder.HasIndex(b => new { b.FarmId, b.Kind });
        builder.HasIndex(b => new { b.FarmId, b.SourceDocumentId, b.Category });

        builder.Property(b => b.RowVersion).IsRowVersion();
    }
}

public class TradeBillPaymentConfiguration : IEntityTypeConfiguration<TradeBillPayment>
{
    public void Configure(EntityTypeBuilder<TradeBillPayment> builder)
    {
        builder.ToTable("TradeBillPayments", "finance");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.BillId).IsRequired();
        builder.Property(p => p.FarmId).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.AmountBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.Method).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Notes).HasMaxLength(500);

        builder.HasOne<TradeBill>()
            .WithMany()
            .HasForeignKey(p => p.BillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.BillId);
        builder.HasIndex(p => p.FarmId);
        builder.HasIndex(p => p.TenantId);

        builder.Property(p => p.RowVersion).IsRowVersion();
    }
}
