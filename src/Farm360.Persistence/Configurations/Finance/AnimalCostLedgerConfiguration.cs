using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class AnimalCostLedgerConfiguration : IEntityTypeConfiguration<AnimalCostLedger>
{
    public void Configure(EntityTypeBuilder<AnimalCostLedger> builder)
    {
        builder.ToTable("AnimalCostLedgers", "finance");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.FarmId).IsRequired();
        builder.Property(l => l.AnimalId).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();

        builder.Property(l => l.AcquisitionCostBdt).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TotalFeedCostBdt).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TotalVetCostBdt).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TotalLaborCostBdt).HasColumnType("decimal(18,2)");
        builder.Property(l => l.TotalOverheadBdt).HasColumnType("decimal(18,2)");
        builder.Property(l => l.SaleRevenueBdt).HasColumnType("decimal(18,2)");

        // Unique: one ledger per animal per tenant
        builder.HasIndex(l => new { l.TenantId, l.AnimalId }).IsUnique();
        builder.HasIndex(l => l.FarmId);

        builder.Property(l => l.RowVersion).IsRowVersion();
    }
}
