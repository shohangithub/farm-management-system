using Farm360.Domain.Livestock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Farm360.Persistence.Configurations.Livestock;

/// <summary>
/// EF Core Fluent API configuration for AnimalPhoto (child of Animal aggregate).
/// Constitution §3.1: Fluent API only.
/// F360-MTA-2026-001 §11 Tenant Storage:
///   PhotoUrl stores the S3 key or presigned URL. S3 prefix:
///   tenants/{tenantId}/animals/{animalId}/photos/{filename}
/// </summary>
public sealed class AnimalPhotoConfiguration : IEntityTypeConfiguration<AnimalPhoto>
{
    public void Configure(EntityTypeBuilder<AnimalPhoto> builder)
    {
        builder.ToTable("AnimalPhotos", "app");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.AnimalId).IsRequired();

        builder.Property(p => p.PhotoUrl)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(p => p.Caption)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.Property(p => p.IsPrimary)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.UploadedBy).IsRequired();
        builder.Property(p => p.UploadedAtUtc).IsRequired();

        builder.HasIndex(p => p.AnimalId)
            .HasDatabaseName("IX_AnimalPhotos_AnimalId");

        // Supports fast lookup of the primary photo per animal for list queries
        builder.HasIndex(p => new { p.AnimalId, p.IsPrimary })
            .HasDatabaseName("IX_AnimalPhotos_AnimalId_IsPrimary");
    }
}
