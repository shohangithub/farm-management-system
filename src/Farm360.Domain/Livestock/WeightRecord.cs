using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.ValueObjects;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Weight measurement record for an animal.
/// Child entity of Animal aggregate — accessed only through Animal.
/// Constitution §2.4: WeightRecord is not an aggregate root; no repository for it.
/// Business rule: RecordedDate >= Animal.DateOfBirth (validated at Animal level).
/// </summary>
public sealed class WeightRecord : BaseEntity
{
    private WeightRecord() { }  // EF Core

    internal WeightRecord(
        Guid id,
        Guid animalId,
        Weight weight,
        DateOnly recordedDate,
        Guid recordedBy,
        string? notes)
        : base(id)
    {
        AnimalId = animalId;
        Weight = weight;
        RecordedDate = recordedDate;
        RecordedBy = recordedBy;
        Notes = notes;
        RecordedAtUtc = DateTime.UtcNow;
    }

    public Guid AnimalId { get; private set; }

    /// <summary>Owned Value Object — EF maps as owned type.</summary>
    public Weight Weight { get; private set; } = null!;

    public DateOnly RecordedDate { get; private set; }
    public Guid RecordedBy { get; private set; }
    public string? Notes { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
}
