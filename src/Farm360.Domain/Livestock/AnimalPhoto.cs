using Farm360.Domain.Common;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Animal photo/image reference.
/// Child entity of Animal aggregate — accessed only through Animal.
/// Storage: S3 (URL stored here; IBlobStorageService manages the file).
/// Constitution §11 Tenant Storage: S3 prefix = tenants/{tenantId}/animals/{animalId}/photos/
/// </summary>
public sealed class AnimalPhoto : BaseEntity
{
    private AnimalPhoto() { }  // EF Core

    internal AnimalPhoto(
        Guid id,
        Guid animalId,
        string photoUrl,
        string? caption,
        bool isPrimary,
        Guid uploadedBy)
        : base(id)
    {
        AnimalId = animalId;
        PhotoUrl = photoUrl;
        Caption = caption;
        IsPrimary = isPrimary;
        UploadedBy = uploadedBy;
        UploadedAtUtc = DateTime.UtcNow;
    }

    public Guid AnimalId { get; private set; }

    /// <summary>Presigned S3 URL or permanent CDN URL to the image.</summary>
    public string PhotoUrl { get; private set; } = string.Empty;

    public string? Caption { get; private set; }

    /// <summary>True if this is the primary display photo for the animal list card.</summary>
    public bool IsPrimary { get; private set; }

    public Guid UploadedBy { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }

    internal void SetAsPrimary() => IsPrimary = true;
    internal void UnsetPrimary() => IsPrimary = false;
}
