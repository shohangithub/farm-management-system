using Farm360.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
{
    public void Configure(EntityTypeBuilder<TenantUser> builder)
    {
        builder.ToTable("TenantUsers", "app");

        builder.HasKey(tu => tu.Id);

        // One user can belong to multiple tenants — unique per TenantId+UserId pair
        builder.HasIndex(tu => new { tu.TenantId, tu.UserId })
            .IsUnique()
            .HasDatabaseName("IX_TenantUsers_TenantId_UserId");

        builder.HasIndex(tu => new { tu.TenantId, tu.Status })
            .HasDatabaseName("IX_TenantUsers_TenantId_Status");

        builder.HasIndex(tu => tu.UserId)
            .HasDatabaseName("IX_TenantUsers_UserId");

        builder.Property(tu => tu.Status)
            .HasConversion<int>()
            .IsRequired();

        // FK to app.Roles
        builder.HasOne(tu => tu.Role)
            .WithMany()
            .HasForeignKey(tu => tu.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK to app.Branches (optional)
        builder.HasOne(tu => tu.Branch)
            .WithMany()
            .HasForeignKey(tu => tu.BranchId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(tu => tu.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
