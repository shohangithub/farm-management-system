using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class FinancialTransactionConfiguration : IEntityTypeConfiguration<FinancialTransaction>
{
    public void Configure(EntityTypeBuilder<FinancialTransaction> builder)
    {
        builder.ToTable("FinancialTransactions", "finance");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.FarmId)
            .IsRequired();
            
        builder.Property(t => t.TenantId)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.AmountBdt)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(t => t.ReferenceId)
            .HasMaxLength(100);

        builder.Property(t => t.Description)
            .HasMaxLength(500);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        // ── Entity link columns (nullable FK references) ────────────────────
        builder.Property(t => t.AnimalId);
        builder.Property(t => t.BatchId);
        builder.Property(t => t.ShedId);

        builder.HasIndex(t => t.FarmId);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.TransactionDate);
        builder.HasIndex(t => t.AnimalId);
    }
}
