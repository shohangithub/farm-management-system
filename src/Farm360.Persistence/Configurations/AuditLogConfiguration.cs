using Farm360.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs", "app");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.EntityName).IsRequired().HasMaxLength(200);
        builder.Property(a => a.EntityId).IsRequired();
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50);
        builder.Property(a => a.CorrelationId).HasMaxLength(50);

        // JSON columns — no length limit (nvarchar(max))
        builder.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValues).HasColumnType("nvarchar(max)");

        builder.HasIndex(a => a.TenantId)
            .HasDatabaseName("IX_AuditLogs_TenantId");

        builder.HasIndex(a => new { a.TenantId, a.EntityName, a.EntityId })
            .HasDatabaseName("IX_AuditLogs_TenantId_Entity");

        builder.HasIndex(a => new { a.TenantId, a.OccurredAtUtc })
            .HasDatabaseName("IX_AuditLogs_TenantId_OccurredAt");
    }
}
