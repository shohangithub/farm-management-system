using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "app");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Body).IsRequired().HasMaxLength(1000);
        builder.Property(n => n.Data).HasColumnType("nvarchar(max)");

        builder.Property(n => n.Type)
            .HasConversion<int>()
            .IsRequired();

        // Key query: unread notifications for a user
        builder.HasIndex(n => new { n.TenantId, n.UserId, n.IsRead })
            .HasDatabaseName("IX_Notifications_UserId_IsRead");

        builder.HasIndex(n => new { n.TenantId, n.UserId, n.CreatedAtUtc })
            .HasDatabaseName("IX_Notifications_UserId_CreatedAt");

        builder.Property(n => n.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}
