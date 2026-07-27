using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Events;

namespace Farm360.Domain.Health;

/// <summary>
/// MortalityRecord Aggregate Root — tracking animal deaths, causes, and economic loss.
/// </summary>
public sealed class MortalityRecord : AuditableEntity, IAggregateRoot
{
    private MortalityRecord() { } // EF Core

    private MortalityRecord(
        Guid id,
        Guid tenantId,
        Guid animalId,
        DateOnly deathDate,
        CauseOfDeath causeOfDeath,
        string? diseaseName,
        string? postMortemNotes,
        decimal? estimatedEconomicLossBdt,
        Guid? diseaseIncidentId,
        Guid recordedByUserId)
        : base(id, tenantId)
    {
        AnimalId = animalId;
        DeathDate = deathDate;
        CauseOfDeath = causeOfDeath;
        DiseaseName = diseaseName;
        PostMortemNotes = postMortemNotes;
        EstimatedEconomicLossBdt = estimatedEconomicLossBdt;
        DiseaseIncidentId = diseaseIncidentId;
        RecordedByUserId = recordedByUserId;
    }

    public Guid AnimalId { get; private set; }
    public DateOnly DeathDate { get; private set; }
    public CauseOfDeath CauseOfDeath { get; private set; }
    public string? DiseaseName { get; private set; }
    public string? PostMortemNotes { get; private set; }
    public decimal? EstimatedEconomicLossBdt { get; private set; }
    public Guid? DiseaseIncidentId { get; private set; }
    public Guid RecordedByUserId { get; private set; }

    public static MortalityRecord Record(
        Guid tenantId,
        Guid animalId,
        DateOnly deathDate,
        CauseOfDeath causeOfDeath,
        string? diseaseName,
        string? postMortemNotes,
        decimal? estimatedEconomicLossBdt,
        Guid? diseaseIncidentId,
        Guid recordedByUserId)
    {
        if (animalId == Guid.Empty)
            throw new ArgumentException("AnimalId is required.", nameof(animalId));

        if (causeOfDeath == CauseOfDeath.Disease && string.IsNullOrWhiteSpace(diseaseName))
            throw new ArgumentException("Disease name is required when cause is Disease.", nameof(diseaseName));

        var record = new MortalityRecord(
            Guid.NewGuid(),
            tenantId,
            animalId,
            deathDate,
            causeOfDeath,
            diseaseName?.Trim(),
            postMortemNotes?.Trim(),
            estimatedEconomicLossBdt,
            diseaseIncidentId,
            recordedByUserId);

        record.RaiseDomainEvent(new MortalityRecordedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            record.Id,
            tenantId,
            animalId,
            causeOfDeath,
            deathDate));

        return record;
    }
}
