using Farm360.Domain.Common;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Breeding / pregnancy record for a female animal.
/// Child entity of Animal aggregate — accessed only through Animal.
/// Constitution §2.4: BreedingRecord is not an aggregate root.
/// Business rules (Constitution §9.4):
///   - PregnancyConfirmDate >= MatingDate
///   - CalvingDate >= MatingDate
///   - Dam != Sire
/// </summary>
public sealed class BreedingRecord : BaseEntity
{
    private BreedingRecord() { }  // EF Core

    internal BreedingRecord(
        Guid id,
        Guid animalId,
        DateOnly matingDate,
        Guid? sireAnimalId,
        string? sireExternalId,
        bool isArtificialInsemination,
        Guid recordedBy)
        : base(id)
    {
        AnimalId = animalId;
        MatingDate = matingDate;
        SireAnimalId = sireAnimalId;
        SireExternalId = sireExternalId;
        IsArtificialInsemination = isArtificialInsemination;
        RecordedBy = recordedBy;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid AnimalId { get; private set; }
    public DateOnly MatingDate { get; private set; }

    /// <summary>FK to Animal for on-platform sires. Null if sire is external.</summary>
    public Guid? SireAnimalId { get; private set; }

    /// <summary>External sire ID string (if sire is not registered on the platform).</summary>
    public string? SireExternalId { get; private set; }

    public bool IsArtificialInsemination { get; private set; }

    public DateOnly? PregnancyConfirmDate { get; private set; }
    public bool IsPregnancyConfirmed { get; private set; }

    /// <summary>Calculated from MatingDate + species gestation period. Stored for query efficiency.</summary>
    public DateOnly? ExpectedCalvingDate { get; private set; }

    public DateOnly? ActualCalvingDate { get; private set; }
    public string? CalvingOutcome { get; private set; }  // Live, Stillborn, Abortion
    public int? CalvesCount { get; private set; }

    public Guid RecordedBy { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    internal void ConfirmPregnancy(DateOnly confirmDate, DateOnly expectedCalvingDate)
    {
        if (confirmDate < MatingDate)
            throw new ArgumentException("Pregnancy confirm date cannot be before mating date.");

        IsPregnancyConfirmed = true;
        PregnancyConfirmDate = confirmDate;
        ExpectedCalvingDate = expectedCalvingDate;
    }

    internal void RecordCalving(DateOnly calvingDate, string outcome, int calvesCount)
    {
        if (calvingDate < MatingDate)
            throw new ArgumentException("Calving date cannot be before mating date.");

        ActualCalvingDate = calvingDate;
        CalvingOutcome = outcome;
        CalvesCount = calvesCount;
    }
}
