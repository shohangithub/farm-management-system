namespace Farm360.Domain.Health.Exceptions;

/// <summary>
/// Base exception for all health domain violations.
/// </summary>
public abstract class HealthDomainException(string message) : Exception(message);

/// <summary>
/// Thrown when attempting to log a duplicate/overlapping treatment for the same drug on an active treatment.
/// PRD §7.4 BRU-HV-03.
/// </summary>
public sealed class OverlappingTreatmentException(string animalTagId, string medicationName)
    : HealthDomainException(
        $"Animal '{animalTagId}' already has an active treatment record for medication '{medicationName}'.");

/// <summary>
/// Thrown when attempting to log a health record for a deceased animal past its death date.
/// PRD §7.4 BRU-HV-04.
/// </summary>
public sealed class DeceasedAnimalHealthRecordException(string animalTagId)
    : HealthDomainException(
        $"Cannot add new health records for deceased animal '{animalTagId}'.");

/// <summary>
/// Thrown when administering a vaccination on a future date.
/// PRD §7.4 BRU-HV-01.
/// </summary>
public sealed class FutureVaccinationDateException(DateOnly administeredDate)
    : HealthDomainException(
        $"Vaccination date '{administeredDate:dd/MM/yyyy}' cannot be in the future.");
