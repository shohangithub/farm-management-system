using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Events;

namespace Farm360.Domain.Health;

/// <summary>
/// DiseaseIncident Aggregate Root — tracking disease outbreaks & health incidents across farms/sheds.
/// </summary>
public sealed class DiseaseIncident : AuditableEntity, IAggregateRoot
{
    private DiseaseIncident() { } // EF Core

    private DiseaseIncident(
        Guid id,
        Guid tenantId,
        Guid farmId,
        Guid? shedId,
        string diseaseName,
        IncidentSeverity severity,
        DateOnly incidentDate,
        string symptoms,
        int affectedAnimalCount,
        string? notes)
        : base(id, tenantId)
    {
        FarmId = farmId;
        ShedId = shedId;
        DiseaseName = diseaseName;
        Severity = severity;
        IncidentDate = incidentDate;
        Symptoms = symptoms;
        AffectedAnimalCount = affectedAnimalCount;
        Notes = notes;
        Status = IncidentStatus.Reported;
    }

    public Guid FarmId { get; private set; }
    public Guid? ShedId { get; private set; }
    public string DiseaseName { get; private set; } = string.Empty;
    public IncidentSeverity Severity { get; private set; }
    public DateOnly IncidentDate { get; private set; }
    public string Symptoms { get; private set; } = string.Empty;
    public int AffectedAnimalCount { get; private set; }
    public IncidentStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public static DiseaseIncident Report(
        Guid tenantId,
        Guid farmId,
        Guid? shedId,
        string diseaseName,
        IncidentSeverity severity,
        DateOnly incidentDate,
        string symptoms,
        int affectedAnimalCount,
        string? notes)
    {
        if (farmId == Guid.Empty)
            throw new ArgumentException("FarmId is required.", nameof(farmId));

        if (string.IsNullOrWhiteSpace(diseaseName))
            throw new ArgumentException("Disease name is required.", nameof(diseaseName));

        if (affectedAnimalCount < 1)
            throw new ArgumentException("Affected animal count must be at least 1.", nameof(affectedAnimalCount));

        var incident = new DiseaseIncident(
            Guid.NewGuid(),
            tenantId,
            farmId,
            shedId,
            diseaseName.Trim(),
            severity,
            incidentDate,
            symptoms?.Trim() ?? string.Empty,
            affectedAnimalCount,
            notes);

        incident.RaiseDomainEvent(new DiseaseIncidentReportedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            incident.Id,
            tenantId,
            farmId,
            diseaseName,
            severity,
            incidentDate));

        return incident;
    }

    public void UpdateStatus(IncidentStatus newStatus, string? notes = null)
    {
        Status = newStatus;
        if (!string.IsNullOrWhiteSpace(notes)) Notes = notes;
    }

    public void UpdateAffectedCount(int count)
    {
        if (count < 0) throw new ArgumentException("Count cannot be negative.", nameof(count));
        AffectedAnimalCount = count;
    }
}
