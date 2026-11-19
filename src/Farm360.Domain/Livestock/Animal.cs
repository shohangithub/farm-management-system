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
    private readonly List<BodyConditionScore> _bcsRecords = [];

    // ── EF Core constructor (required by the ORM) ─────────────────────────────
    private Animal() { }

    private Animal(
        Guid id,
        Guid tenantId,
        Guid farmId,
        AnimalTag tag,
        AnimalSpecies species,
        Guid breedId,
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
        BreedId = breedId;
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

    /// <summary>Foreign key to the Breed master data.</summary>
    public Guid BreedId { get; private set; }

    public AnimalSex Sex { get; private set; }
    
    public Guid? BatchId { get; private set; }

    // ── Dates ─────────────────────────────────────────────────────────────────
    public DateOnly DateOfBirth { get; private set; }
    public AcquisitionType AcquisitionType { get; private set; }
    public DateOnly AcquisitionDate { get; private set; }

    // ── Financial ─────────────────────────────────────────────────────────────
    /// <summary>Purchase price in BDT. Null for born-on-farm animals.</summary>
    public decimal? AcquisitionPriceBdt { get; private set; }

    /// <summary>Sale price in BDT. Set when animal is disposed.</summary>
    public decimal? SalePriceBdt { get; private set; }
    /// <summary>Date of death or slaughter. Null while the animal is alive or was sold.</summary>
    public DateOnly? DeathDate { get; private set; }
    public DateOnly? SaleDate { get; private set; }
    public string? BuyerName { get; private set; }
    public decimal? SaleWeightKg { get; private set; }

    // ── Status ────────────────────────────────────────────────────────────────
    public AnimalStatus Status { get; private set; }
    public string? QuarantineReason { get; private set; }
    public DisposalReason? DisposalReason { get; private set; }
    public string? Notes { get; private set; }

    // ── Presence on the farm (farm rules: see AnimalStatusGroups) ─────────────
    /// <summary>
    /// The day the animal left the farm: SaleDate when Sold, DeathDate when Dead. Null while on
    /// the farm (and for Slaughtered/Transferred, which no command sets and so carry no date).
    /// The animal is NOT on the farm on its exit date itself.
    /// </summary>
    /// <remarks>Same rule as feeding uses (<see cref="Feeding.AnimalFeedingPresence.ExitDate"/>).</remarks>
    public DateOnly? ExitDate => Feeding.AnimalFeedingPresence.ExitDate(this);

    /// <summary>True once the animal is no longer on the farm (Sold, Dead, Slaughtered, Transferred).</summary>
    public bool HasLeftFarm => AnimalStatusGroups.HasLeftFarm(Status);

    /// <summary>
    /// On farm on day D = acquired on or before D and (no exit date or D before the exit date).
    /// An exited animal with no recorded exit date is treated as not on the farm on any day.
    /// </summary>
    public bool IsOnFarmOn(DateOnly day)
    {
        if (day < AcquisitionDate) return false;
        if (!HasLeftFarm) return true;
        return ExitDate is { } exit && day < exit;
    }

    /// <summary>
    /// Guard for recording a new fact dated <paramref name="date"/>: allowed while the animal is on
    /// the farm, or — for an animal that has left — only when dated before its exit date (late entry
    /// of history). Throws <see cref="AnimalHasLeftFarmException"/> naming the sale/death date.
    /// </summary>
    public void EnsureCanRecordOn(DateOnly date, string action)
    {
        if (!HasLeftFarm) return;
        if (ExitDate is { } exit && date < exit) return;
        throw new AnimalHasLeftFarmException(Tag.TagId, Status, ExitDate, action, dateBased: true);
    }

    /// <summary>
    /// Guard for actions that only make sense for an animal physically present today (transfer,
    /// batch assignment, scheduling future work). Throws <see cref="AnimalHasLeftFarmException"/>.
    /// </summary>
    public void EnsureOnFarm(string action)
    {
        if (HasLeftFarm)
            throw new AnimalHasLeftFarmException(Tag.TagId, Status, ExitDate, action, dateBased: false);
    }

    // ── Update Method ─────────────────────────────────────────────────────────
    public void UpdateDetails(
        AnimalTag tag,
        AnimalSpecies species,
        Guid breedId,
        AnimalSex sex,
        DateOnly dateOfBirth,
        AcquisitionType acquisitionType,
        DateOnly acquisitionDate,
        decimal? acquisitionPriceBdt,
        string? notes)
    {
        var oldPrice = AcquisitionPriceBdt;
        var oldTag = Tag?.TagId;
        var oldAcquisitionDate = AcquisitionDate;

        Tag = tag;
        Species = species;
        BreedId = breedId;
        Sex = sex;
        DateOfBirth = dateOfBirth;
        AcquisitionType = acquisitionType;
        AcquisitionDate = acquisitionDate;
        AcquisitionPriceBdt = acquisitionPriceBdt;
        Notes = notes;

        if (oldPrice != acquisitionPriceBdt || oldTag != tag.TagId || oldAcquisitionDate != acquisitionDate)
        {
            RaiseDomainEvent(new AnimalAcquisitionPriceUpdatedEvent(
                Guid.NewGuid(),
                DateTime.UtcNow,
                Id,
                TenantId,
                FarmId,
                Tag.TagId,
                oldPrice,
                acquisitionPriceBdt,
                acquisitionDate));
        }
    }

    // ── Denormalized fields (updated by domain event handlers for query perf) ─
    /// <summary>Most recent weight in kg. Denormalized from last WeightRecord.</summary>
    public decimal? LatestWeightKg { get; private set; }
    public DateOnly? LatestWeightDate { get; private set; }

    /// <summary>
    /// Lifetime Average Daily Gain in kg/day: (newest weight record - oldest weight record) /
    /// days between them, recomputed whenever a weight is recorded. This is NOT bounded to any
    /// reporting period -- a report that needs ADG over a specific date range (e.g. "this month's
    /// growth") must compute it from <see cref="WeightRecords"/> directly rather than read this
    /// field, or its number won't match this animal's growth over that window.
    /// </summary>
    public decimal? AdgKgPerDay { get; private set; }

    public decimal? LatestBcs { get; private set; }

    // ── Child Collections (IReadOnlyCollection — Constitution §3.1) ──────────
    public IReadOnlyCollection<WeightRecord> WeightRecords => _weightRecords.AsReadOnly();
    public IReadOnlyCollection<BreedingRecord> BreedingRecords => _breedingRecords.AsReadOnly();
    public IReadOnlyCollection<AnimalPhoto> Photos => _photos.AsReadOnly();
    public IReadOnlyCollection<AnimalMovement> Movements => _movements.AsReadOnly();
    public IReadOnlyCollection<BodyConditionScore> BcsRecords => _bcsRecords.AsReadOnly();
    
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
        Guid breedId,
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
            breedId,
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
            breedId,
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
    ///   - Active/Quarantined animals: any date. An animal that has left the farm: only a weight
    ///     dated before its exit (sale/death) date — a late entry of history is allowed, a weight
    ///     on/after the exit is not.
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

        EnsureCanRecordOn(recordedDate, "a weight");

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

        EnsureExitNotBeforeCurrentPlacement(saleDate, "sale");

        // The animal leaves its pen/shed on the sale date: close the open movement so pen/shed
        // occupancy and movement-based membership drop it from that day. BatchId is kept (history).
        CloseCurrentMovement(saleDate, soldBy);

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
            FarmId,
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
    public void RecordDeath(DisposalReason cause, DateOnly deathDate, string? notes, Guid? recordedBy = null)
    {
        if (Status is not (AnimalStatus.Active or AnimalStatus.Quarantined))
            throw new InvalidAnimalStateTransitionException(Status.ToString(), AnimalStatus.Dead.ToString());

        if (deathDate < AcquisitionDate)
            throw new InvalidExitDateException(Tag.TagId,
                $"death date {deathDate:dd MMM yyyy} is before the acquisition date {AcquisitionDate:dd MMM yyyy}.");

        EnsureExitNotBeforeCurrentPlacement(deathDate, "death");

        // The animal leaves its pen/shed on the death date (same as a sale). BatchId is kept.
        CloseCurrentMovement(deathDate, recordedBy ?? Guid.Empty);

        Status = AnimalStatus.Dead;
        DisposalReason = cause;
        DeathDate = deathDate;
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
        // A sold/dead animal is not in any pen; moving it would re-open occupancy.
        EnsureOnFarm("a pen/shed transfer");

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
    /// Assigns (or clears) the animal's batch. Only for animals on the farm: an animal that has
    /// left keeps the batch it was in when it left (history), and cannot be moved into another.
    /// </summary>
    public void AssignToBatch(Guid? batchId)
    {
        EnsureOnFarm("a batch assignment");

        BatchId = batchId;
        RaiseDomainEvent(new AnimalAssignedToBatchEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            batchId));
    }

    public BodyConditionScore RecordBodyConditionScore(decimal score, DateOnly recordedDate, Guid evaluatorId, string? notes)
    {
        if (score < 1.0m || score > 5.0m)
            throw new ArgumentException("BCS score must be between 1.0 and 5.0.", nameof(score));

        // Like weights: a score dated before the exit may be entered late; none on/after it.
        EnsureCanRecordOn(recordedDate, "a body condition score");

        var record = new BodyConditionScore(
            Guid.NewGuid(),
            Id,
            score,
            recordedDate,
            evaluatorId,
            notes);

        _bcsRecords.Add(record);

        // Update latest BCS
        var latest = _bcsRecords.OrderByDescending(b => b.RecordedDate).First();
        LatestBcs = latest.Score;

        RaiseDomainEvent(new BcsRecordedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            score,
            recordedDate));

        return record;
    }

    /// <summary>
    /// Adds a photo. If no primary photo exists, the first one becomes primary automatically.
    /// </summary>
    public AnimalPhoto AddPhoto(string photoUrl, string? caption, Guid uploadedBy)
    {
        if (_photos.Count >= 5)
            throw new InvalidOperationException("Maximum 5 photos allowed per animal.");

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

        // A mating can only happen while the dam is on the farm (late entry of an earlier one is fine).
        EnsureCanRecordOn(matingDate, "a mating");

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

        EnsureCanRecordOn(confirmDate, "a pregnancy confirmation");

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

        EnsureCanRecordOn(calvingDate, "a calving");

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
    /// Closes the open pen/shed movement at the exit date (like <see cref="TransferToShed"/> does
    /// when moving), so occupancy queries — which count open movements — drop the animal.
    /// </summary>
    private void CloseCurrentMovement(DateOnly exitDate, Guid removedBy) =>
        CurrentMovement?.MarkAsRemoved(exitDate.ToDateTime(TimeOnly.MinValue), removedBy);

    /// <summary>The exit cannot be dated before the animal was placed in its current pen/shed.</summary>
    private void EnsureExitNotBeforeCurrentPlacement(DateOnly exitDate, string kind)
    {
        if (CurrentMovement is { } current && exitDate.ToDateTime(TimeOnly.MinValue) < current.PlacedAtUtc.Date)
            throw new InvalidExitDateException(Tag.TagId,
                $"{kind} date {exitDate:dd MMM yyyy} is before the animal was moved into its current pen/shed on {DateOnly.FromDateTime(current.PlacedAtUtc):dd MMM yyyy}.");
    }

    /// <summary>
    /// Updates denormalized lifetime ADG from the full weight record history.
    /// ADG = (newest weight record - OLDEST weight record) / days between them -- not the
    /// acquisition weight/date specifically, just whichever weight record is earliest.
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
