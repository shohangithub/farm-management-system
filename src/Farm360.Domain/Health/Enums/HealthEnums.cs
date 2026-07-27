namespace Farm360.Domain.Health.Enums;

/// <summary>
/// Status of a vaccination event.
/// </summary>
public enum VaccinationStatus
{
    Scheduled = 1,
    Completed = 2,
    Overdue   = 3,
    Cancelled = 4,
}

/// <summary>
/// Status of a medical treatment record.
/// </summary>
public enum TreatmentStatus
{
    Ongoing   = 1,
    Completed = 2,
    Failed    = 3,
    Referred  = 4,
}

/// <summary>
/// Severity level of a disease incident/outbreak.
/// </summary>
public enum IncidentSeverity
{
    Mild     = 1,
    Moderate = 2,
    Severe   = 3,
    Critical = 4,
}

/// <summary>
/// Status of a disease incident tracking.
/// </summary>
public enum IncidentStatus
{
    Reported       = 1,
    UnderTreatment = 2,
    Contained      = 3,
    Resolved       = 4,
}

/// <summary>
/// Type of veterinary visit.
/// </summary>
public enum VetVisitType
{
    RoutineCheckup   = 1,
    Emergency        = 2,
    VaccinationDrive = 3,
    Surgery          = 4,
}

/// <summary>
/// Cause of death for a mortality record.
/// </summary>
public enum CauseOfDeath
{
    Disease       = 1,
    Accident      = 2,
    NaturalCauses = 3,
    Unknown       = 4,
    Slaughter     = 5,
}

/// <summary>
/// Frequency for deworming calendar schedules.
/// </summary>
public enum DewormingFrequency
{
    Monthly    = 1,
    Quarterly  = 2,
    BiAnnual   = 3,
    Annual     = 4,
    Custom     = 5,
}
