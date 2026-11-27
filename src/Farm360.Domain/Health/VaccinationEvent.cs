using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Health.Exceptions;

namespace Farm360.Domain.Health;

/// <summary>
/// VaccinationEvent Aggregate Root — tracking scheduled and administered vaccinations per animal.
/// </summary>
public sealed class VaccinationEvent : AuditableEntity, IAggregateRoot
{
    private VaccinationEvent() { } // EF Core

    private VaccinationEvent(
        Guid id,
        Guid tenantId,
        Guid animalId,
        Guid? protocolStepId,
        string vaccineName,
        string batchNumber,
        DateOnly scheduledDate,
        string? notes,
        Guid? inventoryItemId = null,
        decimal? dosageQuantity = null)
        : base(id, tenantId)
    {
        AnimalId = animalId;
        ProtocolStepId = protocolStepId;
        VaccineName = vaccineName;
        BatchNumber = batchNumber;
        ScheduledDate = scheduledDate;
        Notes = notes;
        InventoryItemId = inventoryItemId;
        DosageQuantity = dosageQuantity;
        Status = VaccinationStatus.Scheduled;
    }

    public Guid AnimalId { get; private set; }
    public Guid? ProtocolStepId { get; private set; }
    public string VaccineName { get; private set; } = string.Empty;
    public string BatchNumber { get; private set; } = string.Empty;
    public DateOnly ScheduledDate { get; private set; }
    public DateOnly? AdministeredDate { get; private set; }
    public Guid? AdministeredBy { get; private set; }
    public VaccinationStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? InventoryItemId { get; private set; }

    /// <summary>
    /// The dose per animal. Fixed doses are in the linked item's base unit; a weight-based dose is in
    /// <see cref="DoseUnit"/> (or the <see cref="DoseUnitId"/> stock unit) and is recomputed from the
    /// animal's latest weight when it is given.
    /// </summary>
    public decimal? DosageQuantity { get; private set; }

    // ── Task settings copied from the protocol step when scheduled (steps can be edited later) ──
    public HealthTaskKind Kind { get; private set; } = HealthTaskKind.Medicine;
    public Guid? ProtocolId { get; private set; }
    public string? StepName { get; private set; }
    public string? Instruction { get; private set; }
    public int? RepeatEveryDays { get; private set; }
    public decimal? DoseAmount { get; private set; }
    public decimal? DosePerKg { get; private set; }
    public DoseRounding DoseRounding { get; private set; }
    public decimal? DoseMin { get; private set; }
    public decimal? DoseMax { get; private set; }
    public string? DoseUnit { get; private set; }
    public Guid? DoseUnitId { get; private set; }
    public string? ActionLink { get; private set; }

    /// <summary>The weight the dose was worked out from, and whether it was the farm's fallback weight.</summary>
    public decimal? DoseWeightKg { get; private set; }
    public bool DoseWeightEstimated { get; private set; }

    /// <summary>The dose this one repeats (the previous occurrence of a recurring task).</summary>
    public Guid? PreviousEventId { get; private set; }

    public DateOnly? SkippedDate { get; private set; }
    public string? SkipReason { get; private set; }

    public WeightDoseRule? DoseRule => WeightDoseRule.From(DoseAmount, DosePerKg, DoseRounding, DoseMin, DoseMax, DoseUnit);
    public bool IsRecurring => RepeatEveryDays is > 0;
    public bool IsPending => Status is VaccinationStatus.Scheduled or VaccinationStatus.Overdue;
    public bool IsCareTask => Kind == HealthTaskKind.CareTask;

    /// <summary>The day the task was given or skipped; a repeating task counts its next one from here.</summary>
    public DateOnly? ClosedOn => Status switch
    {
        VaccinationStatus.Completed => AdministeredDate,
        VaccinationStatus.Skipped => SkippedDate,
        _ => null,
    };

    /// <summary>
    /// Schedules a protocol step for one animal: the step's kind, repeat, weight-based dose and
    /// shortcut are copied onto the event, so later edits to the protocol never break a running chain.
    /// </summary>
    public static VaccinationEvent ScheduleFromStep(
        Guid tenantId,
        Guid animalId,
        Guid protocolId,
        VaccinationProtocolStep step,
        DateOnly scheduledDate,
        string? notes)
    {
        ArgumentNullException.ThrowIfNull(step);
        var @event = Schedule(tenantId, animalId, step.Id, step.VaccineName, string.Empty, scheduledDate, notes,
            step.InventoryItemId, step.DosageQuantity);
        @event.ProtocolId = protocolId;
        @event.StepName = step.StepName;
        @event.Instruction = string.IsNullOrWhiteSpace(step.DosageInstruction) ? null : step.DosageInstruction.Trim();
        @event.Kind = step.Kind;
        @event.RepeatEveryDays = step.RepeatEveryDays;
        @event.DoseAmount = step.DoseAmount;
        @event.DosePerKg = step.DosePerKg;
        @event.DoseRounding = step.DoseRounding;
        @event.DoseMin = step.DoseMin;
        @event.DoseMax = step.DoseMax;
        @event.DoseUnit = step.DoseUnit;
        @event.DoseUnitId = step.DoseUnitId;
        @event.ActionLink = step.ActionLink;
        return @event;
    }

    /// <summary>
    /// Works the weight-based dose out for an animal of <paramref name="weightKg"/> kg
    /// (<paramref name="estimated"/> = the farm's fallback weight, the animal was never weighed).
    /// Does nothing for a fixed dose. Returns the dose.
    /// </summary>
    public decimal? ApplyWeight(decimal weightKg, bool estimated)
    {
        if (DoseRule is not { } rule || weightKg <= 0)
            return DosageQuantity;

        DosageQuantity = rule.DoseFor(weightKg);
        DoseWeightKg = weightKg;
        DoseWeightEstimated = estimated;
        return DosageQuantity;
    }

    /// <summary>
    /// The next occurrence of a repeating task, <see cref="RepeatEveryDays"/> after the day this one was
    /// actually given or skipped (so a late dose moves the next one too). Null when the task does not
    /// repeat or is not closed yet. <paramref name="notBefore"/> keeps it from landing in the past.
    /// </summary>
    public VaccinationEvent? CreateNextOccurrence(DateOnly? notBefore = null)
    {
        if (!IsRecurring || ClosedOn is not { } closedOn)
            return null;

        var date = closedOn.AddDays(RepeatEveryDays!.Value);
        if (notBefore is { } floor && date < floor) date = floor;

        var next = new VaccinationEvent(Guid.NewGuid(), TenantId, AnimalId, ProtocolStepId, VaccineName, string.Empty,
            date, null, InventoryItemId, DoseRule is null ? DosageQuantity : null)
        {
            ProtocolId = ProtocolId,
            StepName = StepName,
            Instruction = Instruction,
            Kind = Kind,
            RepeatEveryDays = RepeatEveryDays,
            DoseAmount = DoseAmount,
            DosePerKg = DosePerKg,
            DoseRounding = DoseRounding,
            DoseMin = DoseMin,
            DoseMax = DoseMax,
            DoseUnit = DoseUnit,
            DoseUnitId = DoseUnitId,
            ActionLink = ActionLink,
            PreviousEventId = Id,
        };

        next.RaiseDomainEvent(new VaccinationScheduledEvent(
            Guid.NewGuid(), DateTime.UtcNow, next.Id, TenantId, AnimalId, VaccineName, date));
        return next;
    }

    /// <summary>Not giving this dose, with a reason (e.g. "animal sick, vet advised to wait").</summary>
    public void Skip(DateOnly skippedOn, string reason, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new Farm360.Domain.Exceptions.DomainException("Please give a reason for skipping.");
        if (skippedOn > today)
            throw new Farm360.Domain.Exceptions.DomainException("The skip date cannot be in the future.");
        if (!IsPending)
            throw new Farm360.Domain.Exceptions.DomainException($"Only a task that is still due can be skipped ({VaccineName}).");

        var trimmed = reason.Trim();
        Status = VaccinationStatus.Skipped;
        SkippedDate = skippedOn;
        SkipReason = trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    /// <summary>Moves a task that is still due to another day (today or later).</summary>
    public void Reschedule(DateOnly newDate, DateOnly today)
    {
        if (newDate < today)
            throw new Farm360.Domain.Exceptions.DomainException("The new date cannot be in the past.");
        if (!IsPending)
            throw new Farm360.Domain.Exceptions.DomainException($"Only a task that is still due can be moved ({VaccineName}).");

        ScheduledDate = newDate;
        Status = VaccinationStatus.Scheduled;
    }

    public static VaccinationEvent Schedule(
        Guid tenantId,
        Guid animalId,
        Guid? protocolStepId,
        string vaccineName,
        string batchNumber,
        DateOnly scheduledDate,
        string? notes,
        Guid? inventoryItemId = null,
        decimal? dosageQuantity = null)
    {
        if (animalId == Guid.Empty)
            throw new ArgumentException("AnimalId is required.", nameof(animalId));

        if (string.IsNullOrWhiteSpace(vaccineName))
            throw new ArgumentException("Vaccine name is required.", nameof(vaccineName));

        var @event = new VaccinationEvent(
            Guid.NewGuid(),
            tenantId,
            animalId,
            protocolStepId,
            vaccineName.Trim(),
            batchNumber?.Trim() ?? string.Empty,
            scheduledDate,
            notes,
            inventoryItemId,
            dosageQuantity);

        @event.RaiseDomainEvent(new VaccinationScheduledEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            @event.Id,
            tenantId,
            animalId,
            vaccineName,
            scheduledDate));

        return @event;
    }

    /// <param name="today">The farm's local date (IFarmClock); UTC date when omitted.</param>
    /// <param name="stockQuantity">The quantity to take from stock, in the item's base unit; defaults to <see cref="DosageQuantity"/>.</param>
    public void RecordAdministration(DateOnly administeredDate, Guid administeredBy, string? notes = null, DateOnly? today = null, decimal? stockQuantity = null)
    {
        if (administeredDate > (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new FutureVaccinationDateException(administeredDate);

        // Recording it twice would deduct the vaccine from stock (and cost it) twice.
        if (Status == VaccinationStatus.Completed)
            throw new Farm360.Domain.Exceptions.DomainException($"This {VaccineName} dose was already recorded as given.");
        if (Status == VaccinationStatus.Skipped)
            throw new Farm360.Domain.Exceptions.DomainException($"This {VaccineName} dose was skipped.");

        AdministeredDate = administeredDate;
        AdministeredBy = administeredBy;
        Status = VaccinationStatus.Completed;
        if (!string.IsNullOrWhiteSpace(notes)) Notes = notes;

        RaiseDomainEvent(new VaccinationAdministeredEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            Id,
            TenantId,
            AnimalId,
            VaccineName,
            administeredDate,
            administeredBy,
            // A care task uses no medicine, so nothing is deducted.
            IsCareTask ? null : InventoryItemId,
            IsCareTask ? null : stockQuantity ?? DosageQuantity));
    }

    public void Cancel(string? reason)
    {
        Status = VaccinationStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason)) Notes = reason;
    }

    /// <summary>
    /// The animal left the farm (sold/died) on <paramref name="exitDate"/>: a dose still pending
    /// (Scheduled or Overdue) can never be given, so it is cancelled with a note. Administered and
    /// already-cancelled doses are history and stay untouched. Returns true when it changed.
    /// </summary>
    public bool CancelForAnimalExit(DateOnly exitDate, string reason)
    {
        if (Status is not (VaccinationStatus.Scheduled or VaccinationStatus.Overdue))
            return false;

        Status = VaccinationStatus.Cancelled;
        Notes = AppendExitNote(Notes, reason);
        return true;
    }

    /// <summary>
    /// The animal's sale/death was undone: a dose cancelled because of that exit is due again
    /// (Overdue when its date has passed). <paramref name="reason"/> is the note the exit wrote.
    /// </summary>
    public bool ReopenAfterExitUndone(string reason, DateOnly today)
    {
        if (Status != VaccinationStatus.Cancelled || Notes is null || !Notes.Contains(reason, StringComparison.Ordinal))
            return false;

        Notes = RemoveExitNote(Notes, reason);
        Status = ScheduledDate < today ? VaccinationStatus.Overdue : VaccinationStatus.Scheduled;
        return true;
    }

    private static string? RemoveExitNote(string notes, string reason)
    {
        var cleaned = notes.Replace($" | {reason}", string.Empty, StringComparison.Ordinal)
                           .Replace(reason, string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    // Notes column is 1000 chars: keep the exit reason, trim the older text if needed.
    private static string AppendExitNote(string? notes, string reason)
    {
        const int max = 1000;
        if (string.IsNullOrWhiteSpace(notes)) return reason.Length <= max ? reason : reason[..max];
        var combined = $"{notes} | {reason}";
        return combined.Length <= max ? combined : combined[^max..];
    }

    public void MarkOverdue(DateOnly? today = null)
    {
        if (Status == VaccinationStatus.Scheduled && ScheduledDate < (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
        {
            Status = VaccinationStatus.Overdue;
        }
    }
}
