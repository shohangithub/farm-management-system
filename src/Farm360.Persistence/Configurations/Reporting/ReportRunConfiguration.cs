using Farm360.Domain.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Reporting;

public class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> builder)
    {
        builder.ToTable("ReportRuns", "reporting");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId).IsRequired();
        builder.Property(r => r.ReportKey).IsRequired().HasMaxLength(128);
        builder.Property(r => r.ReportTitle).HasMaxLength(256);
        builder.Property(r => r.RequestedByName).HasMaxLength(256);
        builder.Property(r => r.RequestedAtUtc).IsRequired();

        builder.Property(r => r.Format).HasConversion<int>();
        builder.Property(r => r.Outcome).HasConversion<int>();

        // JSON blobs: the shape differs per report and grows over time, so this is one of the
        // few places where a document column beats columns-per-field.
        builder.Property(r => r.ParametersJson).IsRequired().HasMaxLength(4000);
        builder.Property(r => r.RatesSnapshotJson).HasMaxLength(4000);

        builder.Property(r => r.OutputHash).HasMaxLength(64);
        builder.Property(r => r.ArchivedBlobKey).HasMaxLength(512);
        builder.Property(r => r.FailureReason).HasMaxLength(2000);

        // "What did we run, newest first" is the only query this table serves.
        builder.HasIndex(r => new { r.TenantId, r.RequestedAtUtc })
            .HasDatabaseName("IX_ReportRuns_Tenant_RequestedAt");

        builder.HasIndex(r => new { r.TenantId, r.ReportKey, r.RequestedAtUtc })
            .HasDatabaseName("IX_ReportRuns_Tenant_Key_RequestedAt");

        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
