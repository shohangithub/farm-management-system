using Farm360.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "app");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("IX_Permissions_Code");

        builder.Property(p => p.Module)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.Module)
            .HasDatabaseName("IX_Permissions_Module");

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(500);
    }
}
