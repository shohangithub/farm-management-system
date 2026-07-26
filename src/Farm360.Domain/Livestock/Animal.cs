using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Exceptions;
using Farm360.Domain.Livestock.ValueObjects;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Animal — the core aggregate root for the Livestock bounded context.
///
/// Constitution §2.4 Aggregate Boundaries:
///   Root:     Animal
///   Children: WeightRecord, BreedingRecord, AnimalPhoto
///
/// Constitution §3.1 Domain Layer Rules:
///   - Private setters; state changes ONLY via domain methods.
///   - Static factory method: Animal.Create(...).
///   - Collections exposed as IReadOnlyCollection{T} ONLY.
///   - Domain methods raise events; dispatched after commit by AuditSaveChangesInterceptor.
///
/// F360-MTA-2026-001: ITenantEntity — every query automatically filtered by TenantId.
/// </summary>
public sealed class Animal : AuditableEntity, IAggregateRoot
{
    // ── EF Core private backing fields for child collections ─────────────────
    private readonly List<WeightRecord> _weightRecords = [];
    private readonly List<BreedingRecord> _breedingRecords = [];
    private readonly List<AnimalPhoto> _photos = [];
    private readonly List<AnimalMovement> _movements = [];

    // ── EF Core constructor (required by the ORM) ─────────────────────────────
    private Animal() { }

    private Animal(
        Guid id,
        Guid tenantId,
        Guid farmId,
        AnimalTag tag,
        AnimalSpecies species,
        string breedName,
        AnimalSex sex,
        DateOnly dateOfBirth,
        AcquisitionType acquisitionType,
        DateOnly acquisitionDate,
        decimal? acquisitionPriceBdt,
        string? notes)
        : base(id, tenantId)
    {
        FarmId = farmId;
        Tag = tag;
        Species = species;
        BreedName = breedName;
        Sex = sex;
        DateOfBirth = dateOfBirth;
        AcquisitionType = acquisitionType;
        AcquisitionDate = acquisitionDate;
        AcquisitionPriceBdt = acquisitionPriceBdt;
        Notes = notes;
        Status = AnimalStatus.Active;
    }

    // ── Identity ──────────────────────────────────────────────────────────────
    /// <summary>Farm this animal belongs to. FK to Farms table.</summary>
    public Guid FarmId { get; private set; }

    /// <summary>Owned Value Object — composite tag identifier (TagId + TagType).</summary>
    public AnimalTag Tag { get; private set; } = null!;

    // ── Classification ────────────────────────────────────────────────────────
    public AnimalSpecies Species { get; private set; }

    /// <summary>Free-text breed name (Shahibal, Holstein-Friesian, Black Bengal, etc.).</summary>
    public string BreedName { get; private set; } = string.Empty;

    public AnimalSex Sex { get; private set; }

    // ── Dates ─────────────────────────────────────────────────────────────────
    public DateOnly DateOfBirth { get; private set; }
    public AcquisitionType AcquisitionType { get; private set; }
    public DateOnly AcquisitionDate { get; private set; }

    // ── Financial ─────────────────────────────────────────────────────────────
    /// <summary>Purchase price in BDT. Null for born-on-farm animals.</summary>
    public decimal? AcquisitionPriceBdt { get; private set; }

    /// <summary>Sale price in BDT. Set when animal is disposed.</summary>
    public decimal? SalePriceBdt { get; private set; }
    public DateOnly? SaleDate { get; private set; }
    public string? BuyerName { get; private set; }
    public decimal? SaleWeightKg { get; private set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public AnimalStatus Status { get; private set; }
    public string? QuarantineReason { get; private set; }
    public DisposalReason? DisposalReason { get; private set; }
    public string? Notes { get; private set; }

    // ── Denormalized fields (updated by domain event handlers for query perf) ─
    /// <summary>Most recent weight in kg. Denormalized from last WeightRecord.</summary>
    public decimal? LatestWeightKg { get; private set; }
    public DateOnly? LatestWeightDate { get; private set; }

    /// <summary>Average Daily Gain in kg/day. Recomputed when a weight is recorded.</summary>
    public decimal? AdgKgPerDay { get; private set; }

    // ── Child Collections (IReadOnlyCollection — Constitution §3.1) ──────────
    public IReadOnlyCollection<WeightRecord> WeightRecords => _weightRecords.AsReadOnly();
    public IReadOnlyCollection<BreedingRecord> BreedingRecords => _breedingRecords.AsReadOnly();
    public IReadOnlyCollection<AnimalPhoto> Photos => _photos.AsReadOnly();
    public IReadOnlyCollection<AnimalMovement> Movements => _movements.AsReadOnly();
    
    public AnimalMovement? CurrentMovement => _movements.FirstOrDefault(m => m.RemovedAtUtc == null);

    // ══════════════════════════════════════════════════════════════════════════
    // FACTORY METHOD — the only valid construction path
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates and registers a new animal.
    /// Raises <see cref="AnimalRegisteredEvent"/> — dispatched after DB commit.
    /// Constitution §3.1: Private constructor; only this method creates Animals.
    /// </summary>
    public static Animal Create(
        Guid tenantId,
        Guid farmId,
        AnimalTag tag,
        AnimalSpecies species,
        string breedName,
        AnimalSex sex,
        DateOnly dateOfBirth,
        AcquisitionType acquisitionType,
        DateOnly acquisitionDate,
        decimal? acquisitionPriceBdt,
        string? notes)
    {
        var animal = new Animal(
            Guid.NewGuid(),
            tenantId,
            farmId,
            tag,
            species,
            breedName,
            sex,
            dateOfBirth,
            acquisitionType,
            acquisitionDate,
            acquisitionPriceBdt,
            notes);

        animal.RaiseDomainEvent(new AnimalRegisteredEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            animal.Id,
            tenantId,
            farmId,
            tag.TagId,
            species,
            breedName,
            acquisitionPriceBdt));

        return animal;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // DOMAIN METHODS — state changes ONLY via these methods
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a weight measurement.
    /// Business rules:
    ///   - RecordedDate >= DateOfBirth (Constitution §9.4)
    ///   - Only Active or Quarantined animals can have weight recorded.
    /// Raises <see cref="WeightRecordedEvent"/>.
    /// </summary>
    public WeightRecord RecordWeight(
        Weight weight,
        DateOnly recordedDate,
        Guid recordedBy,
        string? notes)
    {
        if (recordedDate < DateOfBirth)
            throw new InvalidWeightDateException(Tag.TagId, recordedDate, DateOfBirth);

        if (Status is not (AnimalStatus.Active or AnimalStatus.Quarantined))
            throw new InvalidAnimalStateTransitionException(Status.ToString(), "RecordWeight");

        var record = new WeightRecord(
            Guid.NewGuid(),
            Id,
            weight,
            recordedDate,
            recordedBy,
            notes);

        _weightRecords.Add(record);

        // Update denormalized latest weight
        if (LatestWeightDate is null || recordedDate >= LatestWeightDate.Value)
        {
            LatestWeightKg = weight.WeightKg;
            LatestWeightDate = recordedDate;
            RecalculateAdg();
        }

        RaiseDomainEvent(new WeightRecordedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            weight.WeightKg,
            recordedDate));

        return record;
    }

    /// <summary>
    /// Sells the animal.
    /// Business rules:
    ///   - Cannot sell a quarantined animal (Constitution §9.4, F360-AUTH-2026-001 §2.6).
    ///   - SaleDate >= AcquisitionDate.
    ///   - SalePrice > 0.
    /// Raises <see cref="AnimalSoldEvent"/>.
    /// </summary>
    public void Sell(decimal salePriceBdt, DateOnly saleDate, Guid soldBy, string? buyerName, decimal? saleWeightKg)
    {
        if (Status == AnimalStatus.Quarantined)
            throw new AnimalQuarantinedException(Tag.TagId);

        if (Status != AnimalStatus.Active)
            throw new InvalidAnimalStateTransitionException(Status.ToString(), AnimalStatus.Sold.ToString());

        if (saleDate < AcquisitionDate)
            throw new InvalidSaleDateException(Tag.TagId);

        if (salePriceBdt <= 0)
            throw new ArgumentException("Sale price must be greater than zero.", nameof(salePriceBdt));

        Status = AnimalStatus.Sold;
        SalePriceBdt = salePriceBdt;
        SaleDate = saleDate;
        BuyerName = buyerName;
        SaleWeightKg = saleWeightKg;
        DisposalReason = Enums.DisposalReason.Sale;

        RaiseDomainEvent(new AnimalSoldEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            soldBy,
            salePriceBdt,
            saleDate,
            buyerName,
            saleWeightKg));
    }

    /// <summary>
    /// Records the animal's death.
    /// Business rules:
    ///   - Only Active or Quarantined animals can be recorded as dead.
    /// Raises <see cref="AnimalDiedEvent"/>.
    /// </summary>
    public void RecordDeath(DisposalReason cause, DateOnly deathDate, string? notes)
    {
        if (Status is not (AnimalStatus.Active or AnimalStatus.Quarantined))
            throw new InvalidAnimalStateTransitionException(Status.ToString(), AnimalStatus.Dead.ToString());

        Status = AnimalStatus.Dead;
        DisposalReason = cause;
        Notes = notes;

        RaiseDomainEvent(new AnimalDiedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            cause,
            deathDate));
    }

    /// <summary>
    /// Places the animal under quarantine.
    /// Only Active animals can be quarantined.
    /// Raises <see cref="AnimalQuarantinedEvent"/>.
    /// </summary>
    public void Quarantine(string reason)
    {
        if (Status != AnimalStatus.Active)
            throw new InvalidAnimalStateTransitionException(Status.ToString(), AnimalStatus.Quarantined.ToString());

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Quarantine reason is required.", nameof(reason));

        Status = AnimalStatus.Quarantined;
        QuarantineReason = reason;

        RaiseDomainEvent(new AnimalQuarantinedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            reason));
    }

    /// <summary>
    /// Releases the animal from quarantine back to Active.
    /// </summary>
    public void ReleaseFromQuarantine()
    {
        if (Status != AnimalStatus.Quarantined)
            throw new InvalidAnimalStateTransitionException(Status.ToString(), AnimalStatus.Active.ToString());

        Status = AnimalStatus.Active;
        QuarantineReason = null;
    }

    /// <summary>
    /// Transfers the animal to a different shed and pen (or removes assignment).
    /// Raises <see cref="AnimalTransferredEvent"/>.
    /// </summary>
    public void TransferToShed(Guid? toShedId, Guid? toPenId, DateOnly transferDate, Guid transferredBy, string? reason = null)
    {
        if (Status is not (AnimalStatus.Active or AnimalStatus.Quarantined))
            throw new InvalidAnimalStateTransitionException(Status.ToString(), "Transfer");

        var current = CurrentMovement;
        var fromShedId = current?.ShedId;
        
        current?.MarkAsRemoved(transferDate.ToDateTime(TimeOnly.MinValue), transferredBy);

        var movement = new AnimalMovement(
            Guid.NewGuid(),
            TenantId,
            Id,
            toShedId,
            toPenId,
            transferDate.ToDateTime(TimeOnly.MinValue),
            transferredBy,
            reason);

        _movements.Add(movement);

        RaiseDomainEvent(new AnimalTransferredEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            fromShedId,
            toShedId,
            transferDate));
    }

    /// <summary>
    /// Updates the free-text notes on the animal profile.
    /// </summary>
    public void UpdateNotes(string? notes) => Notes = notes;

    /// <summary>
    /// Adds a photo. If no primary photo exists, the first one becomes primary automatically.
    /// </summary>
    public AnimalPhoto AddPhoto(string photoUrl, string? caption, Guid uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(photoUrl))
            throw new ArgumentException("Photo URL cannot be empty.", nameof(photoUrl));

        var isPrimary = _photos.Count == 0 || !_photos.Any(p => p.IsPrimary);
        var photo = new AnimalPhoto(Guid.NewGuid(), Id, photoUrl, caption, isPrimary, uploadedBy);
        _photos.Add(photo);
        return photo;
    }

    /// <summary>
    /// Sets a specific photo as the primary display photo.
    /// </summary>
    public void SetPrimaryPhoto(Guid photoId)
    {
        var target = _photos.FirstOrDefault(p => p.Id == photoId)
            ?? throw new ArgumentException($"Photo '{photoId}' not found on this animal.");

        foreach (var p in _photos) p.UnsetPrimary();
        target.SetAsPrimary();
    }

    /// <summary>
    /// Adds a breeding record for this female animal.
    /// Business rule: Only Female animals can have breeding records.
    /// </summary>
    public BreedingRecord AddBreedingRecord(
        DateOnly matingDate,
        Guid? sireAnimalId,
        string? sireExternalId,
        bool isArtificialInsemination,
        Guid recordedBy)
    {
        if (Sex != AnimalSex.Female)
            throw new InvalidOperationException("Breeding records can only be added to female animals.");

        var record = new BreedingRecord(
            Guid.NewGuid(),
            Id,
            matingDate,
            sireAnimalId,
            sireExternalId,
            isArtificialInsemination,
            recordedBy);

        _breedingRecords.Add(record);

        RaiseDomainEvent(new MatingRecordedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            record.Id,
            matingDate,
            isArtificialInsemination));

        return record;
    }

    /// <summary>
    /// Confirms pregnancy for a breeding record.
    /// </summary>
    public void ConfirmPregnancy(Guid breedingRecordId, DateOnly confirmDate, DateOnly expectedCalvingDate)
    {
        var record = _breedingRecords.FirstOrDefault(r => r.Id == breedingRecordId)
            ?? throw new ArgumentException($"Breeding record '{breedingRecordId}' not found.");

        record.ConfirmPregnancy(confirmDate, expectedCalvingDate);

        RaiseDomainEvent(new PregnancyConfirmedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            breedingRecordId,
            confirmDate,
            expectedCalvingDate));
    }

    /// <summary>
    /// Records calving outcome for a breeding record.
    /// </summary>
    public void RecordCalving(Guid breedingRecordId, DateOnly calvingDate, string outcome, int calvesCount)
    {
        var record = _breedingRecords.FirstOrDefault(r => r.Id == breedingRecordId)
            ?? throw new ArgumentException($"Breeding record '{breedingRecordId}' not found.");

        record.RecordCalving(calvingDate, outcome, calvesCount);

        RaiseDomainEvent(new CalvingRecordedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            breedingRecordId,
            calvingDate,
            outcome,
            calvesCount));
    }

    /// <summary>
    /// Updates denormalized ADG from the weight record history.
    /// ADG = (LatestWeight - AcquisitionWeight) / DaysSinceAcquisition.
    /// Falls back to null if insufficient data.
    /// </summary>
    private void RecalculateAdg()
    {
        if (_weightRecords.Count < 2 || LatestWeightKg is null)
        {
            AdgKgPerDay = null;
            return;
        }

        var oldest = _weightRecords.MinBy(w => w.RecordedDate)!;
        var newest = _weightRecords.MaxBy(w => w.RecordedDate)!;

        int days = newest.RecordedDate.DayNumber - oldest.RecordedDate.DayNumber;
        if (days <= 0)
        {
            AdgKgPerDay = null;
            return;
        }

        AdgKgPerDay = Math.Round(
            (newest.Weight.WeightKg - oldest.Weight.WeightKg) / days,
            3);
    }
}
