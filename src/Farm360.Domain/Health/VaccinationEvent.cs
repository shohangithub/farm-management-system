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
    public decimal? DosageQuantity { get; private set; }

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
    public void RecordAdministration(DateOnly administeredDate, Guid administeredBy, string? notes = null, DateOnly? today = null)
    {
        if (administeredDate > (today ?? DateOnly.FromDateTime(DateTime.UtcNow)))
            throw new FutureVaccinationDateException(administeredDate);

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
            InventoryItemId,
            DosageQuantity));
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
