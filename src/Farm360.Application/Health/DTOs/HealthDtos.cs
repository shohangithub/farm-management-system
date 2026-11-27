using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Health.DTOs;

public sealed record VaccinationProtocolDto(
    Guid Id,
    string Title,
    AnimalSpecies TargetSpecies,
    string? Description,
    bool IsActive,
    bool IsDeworming,
    IReadOnlyList<VaccinationProtocolStepDto> Steps,
    bool AutoStartForPurchased = false,
    int SpanDays = 0
);

public sealed record VaccinationProtocolStepDto(
    Guid Id,
    int StepOrder,
    string StepName,
    int TargetAgeDays,
    string VaccineName,
    string DosageInstruction,
    Guid? InventoryItemId,
    decimal? DosageQuantity,
    HealthTaskKind Kind = HealthTaskKind.Medicine,
    int? RepeatEveryDays = null,
    decimal? DoseAmount = null,
    decimal? DosePerKg = null,
    DoseRounding DoseRounding = DoseRounding.None,
    decimal? DoseMin = null,
    decimal? DoseMax = null,
    string? DoseUnit = null,
    Guid? DoseUnitId = null,
    string? ActionLink = null
);

public sealed record VaccinationEventDto(
    Guid Id,
    Guid AnimalId,
    string VaccineName,
    string BatchNumber,
    DateOnly ScheduledDate,
    DateOnly? AdministeredDate,
    VaccinationStatus Status,
    string? Notes,
    // The animal's ear-tag number; null when the animal is unknown.
    string? AnimalTag = null,
    HealthTaskKind Kind = HealthTaskKind.Medicine,
    string? StepName = null,
    decimal? DosageQuantity = null,
    string? DoseUnit = null,
    bool WeightBased = false,
    decimal? DoseWeightKg = null,
    bool DoseWeightEstimated = false,
    int? RepeatEveryDays = null,
    DateOnly? SkippedDate = null,
    string? SkipReason = null
);

public sealed record DewormingCalendarEventDto(
    Guid EventId,
    Guid AnimalId,
    string AnimalTag,
    string VaccineName,
    DateOnly ScheduledDate,
    VaccinationStatus Status
);

public sealed record MedicalTreatmentDto(
    Guid Id,
    Guid AnimalId,
    // The animal's ear-tag number; null when the animal is unknown.
    string? AnimalTag,
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
    string? Notes,
    Guid? InventoryItemId,
    decimal? ConsumptionQuantity
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
    Guid RecordedByUserId,
    // The animal's ear-tag number; null when the animal is unknown.
    string? AnimalTag = null
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
