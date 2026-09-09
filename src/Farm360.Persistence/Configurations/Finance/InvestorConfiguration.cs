using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class InvestorConfiguration : IEntityTypeConfiguration<Investor>
{
    public void Configure(EntityTypeBuilder<Investor> builder)
    {
        builder.ToTable("Investors", "finance");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.FarmId).IsRequired();
        builder.Property(i => i.TenantId).IsRequired();

        builder.Property(i => i.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.Email).HasMaxLength(150);
        builder.Property(i => i.Phone).HasMaxLength(50);
        builder.Property(i => i.NationalId).HasMaxLength(50);

        builder.Property(i => i.TotalInvestedBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.TotalWithdrawnBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.TotalProfitPaidBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(i => i.AgreedProfitSharePercentage).HasColumnType("decimal(5,2)");

        builder.Property(i => i.Notes).HasMaxLength(1000);
        builder.Property(i => i.IsActive).IsRequired();

        builder.HasIndex(i => i.FarmId);
        builder.HasIndex(i => i.TenantId);

        builder.Property(i => i.RowVersion).IsRowVersion();

        // Configure child collection
        builder.HasMany(i => i.Transactions)
            .WithOne()
            .HasForeignKey(t => t.InvestorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Access the private backing field directly
        builder.Navigation(i => i.Transactions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public class InvestorTransactionConfiguration : IEntityTypeConfiguration<InvestorTransaction>
{
    public void Configure(EntityTypeBuilder<InvestorTransaction> builder)
    {
        builder.ToTable("InvestorTransactions", "finance");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.InvestorId).IsRequired();
        builder.Property(t => t.FarmId).IsRequired();
        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.AmountBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(t => t.ReferenceId).HasMaxLength(100);
        builder.Property(t => t.Notes).HasMaxLength(500);

        builder.HasIndex(t => t.InvestorId);
        builder.HasIndex(t => t.FarmId);
        builder.HasIndex(t => t.TenantId);
        builder.HasIndex(t => t.TransactionDate);

        builder.Property(t => t.RowVersion).IsRowVersion();
    }
}
