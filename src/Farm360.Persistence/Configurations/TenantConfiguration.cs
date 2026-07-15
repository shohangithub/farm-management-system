using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", "app");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.Slug)
            .IsUnique()
            .HasDatabaseName("IX_Tenants_Slug");

        builder.Property(t => t.LogoUrl).HasMaxLength(500);
        builder.Property(t => t.PrimaryColor).HasMaxLength(7); // "#RRGGBB"
        builder.Property(t => t.TimeZone).HasMaxLength(50);
        builder.Property(t => t.DefaultCurrency).HasMaxLength(3);

        builder.Property(t => t.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(t => t.SubscriptionTier)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(t => t.Status)
            .HasDatabaseName("IX_Tenants_Status");

        builder.HasIndex(t => new { t.Status, t.SubscriptionExpiresAt })
            .HasDatabaseName("IX_Tenants_Status_ExpiresAt");

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.UpdatedAtUtc).IsRequired();

        // Soft delete — no global query filter on Tenant itself (it IS the partition boundary)
        builder.Property(t => t.IsDeleted).HasDefaultValue(false);
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Navigation: Organizations
        builder.HasMany(t => t.Organizations)
            .WithOne()
            .HasForeignKey(o => o.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
