using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Health.ValueObjects;

namespace Farm360.Domain.Health;

/// <summary>
/// MedicalTreatment Aggregate Root — medical diagnosis, treatment, medication dosage, withdrawal period, and cost logging per animal.
/// </summary>
public sealed class MedicalTreatment : AuditableEntity, IAggregateRoot
{
    private MedicalTreatment() { } // EF Core

    private MedicalTreatment(
        Guid id,
        Guid tenantId,
        Guid animalId,
        string diagnosis,
        string medicationName,
        Dosage dosage,
        WithdrawalPeriod withdrawalPeriod,
        DateOnly startDate,
        DateOnly? endDate,
        decimal costBdt,
        string? veterinarianName,
        string? notes,
        Guid? inventoryItemId = null,
        decimal? consumptionQuantity = null)
        : base(id, tenantId)
    {
        AnimalId = animalId;
        Diagnosis = diagnosis;
        MedicationName = medicationName;
        Dosage = dosage;
        WithdrawalPeriod = withdrawalPeriod;
        StartDate = startDate;
        EndDate = endDate;
        CostBdt = costBdt;
        VeterinarianName = veterinarianName;
        Notes = notes;
        InventoryItemId = inventoryItemId;
        ConsumptionQuantity = consumptionQuantity;
        Status = TreatmentStatus.Ongoing;
    }

    public Guid AnimalId { get; private set; }
    public string Diagnosis { get; private set; } = string.Empty;
    public string MedicationName { get; private set; } = string.Empty;
    public Dosage Dosage { get; private set; } = null!;
    public WithdrawalPeriod WithdrawalPeriod { get; private set; } = null!;
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public decimal CostBdt { get; private set; }
    public string? VeterinarianName { get; private set; }
    public TreatmentStatus Status { get; private set; }
    public string? Notes { get; private set; }
    public Guid? InventoryItemId { get; private set; }
    public decimal? ConsumptionQuantity { get; private set; }

    public static MedicalTreatment LogTreatment(
        Guid tenantId,
        Guid animalId,
        string diagnosis,
        string medicationName,
        Dosage dosage,
        WithdrawalPeriod withdrawalPeriod,
        DateOnly startDate,
        DateOnly? endDate,
        decimal costBdt,
        string? veterinarianName,
        string? notes,
        Guid? inventoryItemId = null,
        decimal? consumptionQuantity = null)
    {
        if (animalId == Guid.Empty)
            throw new ArgumentException("AnimalId is required.", nameof(animalId));

        if (string.IsNullOrWhiteSpace(diagnosis))
            throw new ArgumentException("Diagnosis is required.", nameof(diagnosis));

        if (string.IsNullOrWhiteSpace(medicationName))
            throw new ArgumentException("Medication name is required.", nameof(medicationName));

        if (costBdt < 0)
            throw new ArgumentException("Treatment cost cannot be negative.", nameof(costBdt));

        var treatment = new MedicalTreatment(
            Guid.NewGuid(),
            tenantId,
            animalId,
            diagnosis.Trim(),
            medicationName.Trim(),
            dosage,
            withdrawalPeriod ?? WithdrawalPeriod.None,
            startDate,
            endDate,
            costBdt,
            veterinarianName?.Trim(),
            notes,
            inventoryItemId,
            consumptionQuantity);

        treatment.RaiseDomainEvent(new TreatmentLoggedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            treatment.Id,
            tenantId,
            animalId,
            diagnosis,
            medicationName,
            costBdt,
            startDate,
            inventoryItemId,
            consumptionQuantity));

        return treatment;
    }

    public void CompleteTreatment(DateOnly endDate, string? summaryNotes = null)
    {
        EndDate = endDate;
        Status = TreatmentStatus.Completed;
        if (!string.IsNullOrWhiteSpace(summaryNotes)) Notes = summaryNotes;
    }

    /// <summary>
    /// The animal left the farm (sold/died) on <paramref name="exitDate"/>: an ongoing course has
    /// no future doses any more. It is closed as Completed with its end date capped at the exit
    /// date (never before its start) and a note saying why. Finished treatments are untouched;
    /// the record and its cost stay (history). Returns true when it changed.
    /// </summary>
    public bool CloseForAnimalExit(DateOnly exitDate, string reason)
    {
        if (Status != TreatmentStatus.Ongoing)
            return false;

        var end = exitDate < StartDate ? StartDate : exitDate;
        if (EndDate is null || EndDate.Value > end)
            EndDate = end;

        Status = TreatmentStatus.Completed;
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

    public void MarkFailed(string reason)
    {
        Status = TreatmentStatus.Failed;
        Notes = reason;
    }

    public void UpdateDetails(
        string diagnosis,
        string medicationName,
        Dosage dosage,
        WithdrawalPeriod withdrawalPeriod,
        DateOnly startDate,
        DateOnly? endDate,
        decimal costBdt,
        string? veterinarianName,
        string? notes,
        TreatmentStatus status,
        Guid? inventoryItemId = null,
        decimal? consumptionQuantity = null)
    {
        if (string.IsNullOrWhiteSpace(diagnosis))
            throw new ArgumentException("Diagnosis is required.", nameof(diagnosis));

        if (string.IsNullOrWhiteSpace(medicationName))
            throw new ArgumentException("Medication name is required.", nameof(medicationName));

        if (costBdt < 0)
            throw new ArgumentException("Treatment cost cannot be negative.", nameof(costBdt));

        Diagnosis = diagnosis.Trim();
        MedicationName = medicationName.Trim();
        Dosage = dosage;
        WithdrawalPeriod = withdrawalPeriod ?? WithdrawalPeriod.None;
        StartDate = startDate;
        EndDate = endDate;
        CostBdt = costBdt;
        VeterinarianName = veterinarianName?.Trim();
        Notes = notes;
        Status = status;
        InventoryItemId = inventoryItemId;
        ConsumptionQuantity = consumptionQuantity;
    }
}
