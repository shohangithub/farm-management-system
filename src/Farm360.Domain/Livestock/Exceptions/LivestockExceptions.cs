namespace Farm360.Domain.Livestock.Exceptions;

/// <summary>
/// Base exception for all livestock domain rule violations.
/// Constitution §10: Domain exceptions are thrown by entities and domain services.
/// GlobalExceptionMiddleware maps these to HTTP 422 Unprocessable Entity.
/// </summary>
public abstract class LivestockDomainException(string message) : Exception(message);

/// <summary>
/// Thrown when a state transition is attempted that the domain state machine does not allow.
/// Example: Trying to sell a quarantined animal without releasing it first.
/// HTTP 422.
/// </summary>
public sealed class InvalidAnimalStateTransitionException(
    string currentStatus, string requestedStatus)
    : LivestockDomainException(
        $"Cannot transition animal from '{currentStatus}' to '{requestedStatus}'.");

/// <summary>
/// Thrown when business logic is attempted on a quarantined animal that is not permitted.
/// Example: Selling, transferring, or slaughtering a quarantined animal.
/// HTTP 422.
/// </summary>
public sealed class AnimalQuarantinedException(string tagId)
    : LivestockDomainException(
        $"Animal '{tagId}' is currently quarantined and cannot be sold or transferred.");

/// <summary>
/// Thrown when a weight record date is earlier than the animal's date of birth.
/// Constitution §9.4: WeightDate >= DateOfBirth.
/// HTTP 422.
/// </summary>
public sealed class InvalidWeightDateException(string tagId, DateOnly weightDate, DateOnly dateOfBirth)
    : LivestockDomainException(
        $"Weight record date {weightDate:dd/MM/yyyy} for animal '{tagId}' cannot be before date of birth {dateOfBirth:dd/MM/yyyy}.");

/// <summary>
/// Thrown when a sale date is earlier than the acquisition date.
/// Constitution §9.4: SaleDate >= AcquisitionDate.
/// HTTP 422.
/// </summary>
public sealed class InvalidSaleDateException(string tagId)
    : LivestockDomainException(
        $"Sale date for animal '{tagId}' cannot be before the acquisition date.");
