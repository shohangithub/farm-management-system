using Farm360.Domain.Common;
using Farm360.Domain.Health.Enums;

namespace Farm360.Domain.Health.Events;

public sealed record VaccinationScheduledEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid VaccinationEventId,
    Guid TenantId,
    Guid AnimalId,
    string VaccineName,
    DateOnly ScheduledDate
) : IDomainEvent;

public sealed record VaccinationAdministeredEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid VaccinationEventId,
    Guid TenantId,
    Guid AnimalId,
    string VaccineName,
    DateOnly AdministeredDate,
    Guid AdministeredBy
) : IDomainEvent;

public sealed record TreatmentLoggedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid MedicalTreatmentId,
    Guid TenantId,
    Guid AnimalId,
    string Diagnosis,
    string MedicationName,
    decimal CostBdt,
    DateOnly StartDate
) : IDomainEvent;

public sealed record DiseaseIncidentReportedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid DiseaseIncidentId,
    Guid TenantId,
    Guid FarmId,
    string DiseaseName,
    IncidentSeverity Severity,
    DateOnly IncidentDate
) : IDomainEvent;
