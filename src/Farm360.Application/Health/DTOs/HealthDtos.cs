using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Health.DTOs;

public sealed record VaccinationProtocolDto(
    Guid Id,
    string Title,
    AnimalSpecies TargetSpecies,
    string? Description,
    bool IsActive,
    IReadOnlyList<VaccinationProtocolStepDto> Steps
);

public sealed record VaccinationProtocolStepDto(
    Guid Id,
    int StepOrder,
    string StepName,
    int TargetAgeDays,
    string VaccineName,
    string DosageInstruction
);

public sealed record VaccinationEventDto(
    Guid Id,
    Guid AnimalId,
    string VaccineName,
    string BatchNumber,
    DateOnly ScheduledDate,
    DateOnly? AdministeredDate,
    VaccinationStatus Status,
    string? Notes
);

public sealed record MedicalTreatmentDto(
    Guid Id,
    Guid AnimalId,
    string? AnimalTagId,
    string Diagnosis,
    string MedicationName,
    decimal DosageAmount,
    string DosageUnit,
    int MilkWithdrawalDays,
    int MeatWithdrawalDays,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal CostBdt,
    string? VeterinarianName,
    TreatmentStatus Status,
    string? Notes
);

public sealed record DiseaseIncidentDto(
    Guid Id,
    Guid FarmId,
    Guid? ShedId,
    string DiseaseName,
    IncidentSeverity Severity,
    DateOnly IncidentDate,
    string Symptoms,
    int AffectedAnimalCount,
    IncidentStatus Status,
    string? Notes
);

public sealed record DiseaseIncidentDetailDto(
    Guid Id,
    Guid FarmId,
    Guid? ShedId,
    string DiseaseName,
    IncidentSeverity Severity,
    DateOnly IncidentDate,
    string Symptoms,
    int AffectedAnimalCount,
    IncidentStatus Status,
    string? Notes,
    IReadOnlyList<Guid> AffectedAnimalIds
);

public sealed record MortalityRecordDto(
    Guid Id,
    Guid AnimalId,
    DateOnly DeathDate,
    CauseOfDeath CauseOfDeath,
    string? DiseaseName,
    string? PostMortemNotes,
    decimal? EstimatedEconomicLossBdt,
    Guid? DiseaseIncidentId,
    Guid RecordedByUserId
);

public sealed record VetVisitDto(
    Guid Id,
    Guid FarmId,
    string VetName,
    DateOnly VisitDate,
    VetVisitType VisitType,
    string? Purpose,
    string? Findings,
    string? Recommendations,
    decimal? CostBdt,
    DateOnly? NextVisitDate
);

public sealed record MilkWithdrawalDto(
    Guid AnimalId,
    string AnimalTag,
    Guid TreatmentId,
    string MedicationName,
    DateOnly TreatmentStartDate,
    int WithdrawalDays,
    DateOnly SafeToMilkDate
);

public sealed record HealthDashboardDto(
    int VaccinationsDueThisWeek,
    int VaccinationsOverdue,
    int ActiveTreatments,
    int ActiveDiseaseIncidents,
    int RecentMortalityCount,
    decimal MonthlyHealthCostBdt
);
