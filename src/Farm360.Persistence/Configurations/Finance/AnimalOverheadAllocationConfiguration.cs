using Farm360.Domain.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Finance;

public class AnimalOverheadAllocationConfiguration : IEntityTypeConfiguration<AnimalOverheadAllocation>
{
    public void Configure(EntityTypeBuilder<AnimalOverheadAllocation> builder)
    {
        builder.ToTable("AnimalOverheadAllocations", "finance");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.AnimalId).IsRequired();
        builder.Property(a => a.FarmId).IsRequired();
        builder.Property(a => a.SourceTransactionId).IsRequired();
        builder.Property(a => a.PeriodStart).IsRequired();
        builder.Property(a => a.PeriodEnd).IsRequired();

        builder.Property(a => a.AllocatedAmountBdt).HasColumnType("decimal(18,4)");
        builder.Property(a => a.HeadDays).HasColumnType("decimal(18,4)");
        builder.Property(a => a.WeightAtAllocationKg).HasColumnType("decimal(18,4)");
        builder.Property(a => a.ShareFactor).HasColumnType("decimal(18,6)");

        builder.Property(a => a.Category).HasConversion<int>();
        builder.Property(a => a.Bucket).HasConversion<int>();
        builder.Property(a => a.Method).HasConversion<int>();

        // Recomputing a ledger's buckets: all of one animal's allocations.
        builder.HasIndex(a => new { a.TenantId, a.AnimalId })
            .HasDatabaseName("IX_AnimalOverheadAllocations_Tenant_Animal");

        // Finding what a period has already allocated, for the idempotency check.
        builder.HasIndex(a => new { a.FarmId, a.PeriodStart, a.PeriodEnd })
            .HasDatabaseName("IX_AnimalOverheadAllocations_Farm_Period");

        // One row per expense per animal. The service skips transactions it has already
        // allocated; this is the database-level guarantee behind that check, so two concurrent
        // runs cannot both decide a transaction is unallocated and post it twice.
        builder.HasIndex(a => new { a.SourceTransactionId, a.AnimalId })
            .IsUnique()
            .HasDatabaseName("UX_AnimalOverheadAllocations_Transaction_Animal");

        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
