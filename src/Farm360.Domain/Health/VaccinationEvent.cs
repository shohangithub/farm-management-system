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
        string? notes)
        : base(id, tenantId)
    {
        AnimalId = animalId;
        ProtocolStepId = protocolStepId;
        VaccineName = vaccineName;
        BatchNumber = batchNumber;
        ScheduledDate = scheduledDate;
        Notes = notes;
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

    public static VaccinationEvent Schedule(
        Guid tenantId,
        Guid animalId,
        Guid? protocolStepId,
        string vaccineName,
        string batchNumber,
        DateOnly scheduledDate,
        string? notes)
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
            notes);

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

    public void RecordAdministration(DateOnly administeredDate, Guid administeredBy, string? notes = null)
    {
        if (administeredDate > DateOnly.FromDateTime(DateTime.UtcNow))
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
            administeredBy));
    }

    public void Cancel(string? reason)
    {
        Status = VaccinationStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason)) Notes = reason;
    }

    public void MarkOverdue()
    {
        if (Status == VaccinationStatus.Scheduled && ScheduledDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            Status = VaccinationStatus.Overdue;
        }
    }
}
