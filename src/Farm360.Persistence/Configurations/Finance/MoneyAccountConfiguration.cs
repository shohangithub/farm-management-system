using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class MoneyAccountConfiguration : IEntityTypeConfiguration<MoneyAccount>
{
    public void Configure(EntityTypeBuilder<MoneyAccount> builder)
    {
        builder.ToTable("MoneyAccounts", "finance");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FarmId).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.Name).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ProviderName).HasMaxLength(100);
        builder.Property(a => a.AccountNumber).HasMaxLength(50);
        builder.Property(a => a.Branch).HasMaxLength(100);
        builder.Property(a => a.Notes).HasMaxLength(500);
        builder.Property(a => a.IsDefault).IsRequired();
        builder.Property(a => a.IsActive).IsRequired();
        builder.Property(a => a.RowVersion).IsRowVersion();

        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.FarmId);

        // Exactly one live default account per farm.
        builder.HasIndex(a => a.FarmId)
            .HasDatabaseName("UX_MoneyAccounts_Farm_Default")
            .IsUnique()
            .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");
    }
}
