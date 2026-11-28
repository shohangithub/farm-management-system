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

        builder.Property(t => t.IsAutomated)
            .HasDefaultValue(false);

        builder.Property(t => t.SourceModule)
            .HasMaxLength(50);

        builder.Property(t => t.LedgerVetPostedAnimalId);
        builder.Property(t => t.LedgerVetPostedBdt)
            .HasPrecision(18, 2);

        // Stored as text like Type/Category. Existing rows are cash postings, hence the default.
        builder.Property(t => t.Settlement)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(Farm360.Domain.Finance.Enums.SettlementAccount.Cash)
            .HasSentinel(Farm360.Domain.Finance.Enums.SettlementAccount.Cash)
            .IsRequired();

        builder.Property(t => t.SourceDocumentId);

        builder.Ignore(t => t.AffectsCash);
        builder.Ignore(t => t.MovesMoneyAccount);
        builder.Ignore(t => t.IsAccountTransfer);

        // The money account a cash posting went through (default account when not chosen) and the
        // other half of a pair (transfer legs; a partner-paid spend and its capital contribution).
        builder.Property(t => t.MoneyAccountId);
        builder.Property(t => t.LinkedTransactionId);
        builder.HasOne<MoneyAccount>()
            .WithMany()
            .HasForeignKey(t => t.MoneyAccountId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(t => new { t.FarmId, t.MoneyAccountId });

        builder.HasIndex(t => t.FarmId);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.TransactionDate);
        builder.HasIndex(t => t.AnimalId);
        builder.HasIndex(t => t.BatchId);
        // Idempotency lookups for automated postings: (farm, source document, category).
        builder.HasIndex(t => new { t.FarmId, t.SourceDocumentId, t.Category });
    }
}
