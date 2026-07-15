using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations", "app");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Name).IsRequired().HasMaxLength(200);
        builder.Property(o => o.Description).HasMaxLength(1000);
        builder.Property(o => o.Address).HasMaxLength(500);
        builder.Property(o => o.Phone).HasMaxLength(20);
        builder.Property(o => o.Email).HasMaxLength(200);

        builder.Property(o => o.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(o => o.TenantId)
            .HasDatabaseName("IX_Organizations_TenantId");

        builder.HasMany(o => o.Branches)
            .WithOne(b => b.Organization)
            .HasForeignKey(b => b.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        // Concurrency token
        builder.Property(o => o.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
