using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class TenantSubscriptionRecordConfiguration : IEntityTypeConfiguration<TenantSubscriptionRecord>
{
    public void Configure(EntityTypeBuilder<TenantSubscriptionRecord> builder)
    {
        builder.ToTable("TenantSubscriptions", "app");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.TenantId)
            .IsRequired();

        builder.Property(s => s.Tier)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.BillingCycle)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(s => s.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(s => s.Currency)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(s => s.StartedAtUtc)
            .IsRequired();

        builder.Property(s => s.PaymentMethod)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.PaymentReference)
            .HasMaxLength(100);

        builder.Property(s => s.Status)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(s => s.InvoiceNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Notes)
            .HasMaxLength(500);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(s => new { s.TenantId, s.CreatedAtUtc })
            .HasDatabaseName("IX_TenantSubscriptions_TenantId_CreatedAt");

        builder.HasIndex(s => s.InvoiceNumber)
            .IsUnique()
            .HasDatabaseName("IX_TenantSubscriptions_InvoiceNumber");
    }
}
