using Farm360.Domain.Identity;
using Farm360.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable("Invitations", "app");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Phone).HasMaxLength(32);
        builder.Property(i => i.Email).HasMaxLength(256);
        builder.Property(i => i.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(i => i.Status).HasConversion<int>().IsRequired();

        // Anonymous accept looks invitations up by token hash only.
        builder.HasIndex(i => i.TokenHash)
            .IsUnique()
            .HasDatabaseName("IX_Invitations_TokenHash");

        builder.HasIndex(i => new { i.TenantId, i.Status })
            .HasDatabaseName("IX_Invitations_TenantId_Status");

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(i => i.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Property(i => i.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
