using Farm360.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Inventory;

public sealed class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.ToTable("StockTransactions", "inventory");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Quantity)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.UnitCostBdt)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.BalanceAfter)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.InvoiceNumber)
            .HasMaxLength(100);

        builder.Property(x => x.BatchNumber)
            .HasMaxLength(100);

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.RecordedBy)
            .HasMaxLength(150);

        builder.HasIndex(x => new { x.TenantId, x.InventoryItemId, x.TransactionDate });
    }
}
