using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class LoanRecordConfiguration : IEntityTypeConfiguration<LoanRecord>
{
    public void Configure(EntityTypeBuilder<LoanRecord> builder)
    {
        builder.ToTable("LoanRecords", "finance");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.FarmId).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();

        builder.Property(l => l.LenderName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(l => l.PrincipalAmountBdt).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(l => l.InterestRatePercent).HasColumnType("decimal(5,2)").IsRequired();
        builder.Property(l => l.TotalRepaidBdt).HasColumnType("decimal(18,2)");
        
        builder.Property(l => l.Schedule)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(l => l.Notes).HasMaxLength(1000);

        builder.HasIndex(l => l.FarmId);
        builder.HasIndex(l => l.TenantId);

        builder.Property(l => l.RowVersion).IsRowVersion();
    }
}
