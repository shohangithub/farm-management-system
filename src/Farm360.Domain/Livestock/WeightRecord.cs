using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;
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
        string? notes,
        WeightMethod method = WeightMethod.Scale,
        decimal? lengthInch = null,
        decimal? girthInch = null)
        : base(id)
    {
        Method = method;
        LengthInch = method == WeightMethod.Tape ? lengthInch : null;
        GirthInch = method == WeightMethod.Tape ? girthInch : null;
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

    /// <summary>Scale (kg entered) or Tape (kg estimated from length and girth by <see cref="TapeWeight"/>).</summary>
    public WeightMethod Method { get; private set; } = WeightMethod.Scale;

    /// <summary>Tape body length in inches (shoulder point to pin bone); null for a scale weight.</summary>
    public decimal? LengthInch { get; private set; }

    /// <summary>Tape heart girth in inches (around the chest behind the front legs); null for a scale weight.</summary>
    public decimal? GirthInch { get; private set; }
}
