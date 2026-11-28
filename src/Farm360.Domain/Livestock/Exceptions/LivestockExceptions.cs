using Farm360.Domain.Exceptions;

namespace Farm360.Domain.Livestock.Exceptions;

/// <summary>
/// Base exception for all livestock domain rule violations.
/// Constitution §10: Domain exceptions are thrown by entities and domain services.
/// GlobalExceptionMiddleware maps these to HTTP 422 Unprocessable Entity.
/// </summary>
public abstract class LivestockDomainException(string message) : DomainException(message);

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

/// <summary>
/// Thrown when a new fact (weight, mating, transfer, treatment…) is recorded for an animal that has
/// left the farm, or dated on/after its exit (sale or death) date. Records dated before the exit
/// may still be entered late. Farm rule: see <see cref="AnimalStatusGroups"/>.
/// HTTP 422.
/// </summary>
public sealed class AnimalHasLeftFarmException : LivestockDomainException
{
    public AnimalHasLeftFarmException(string tagId, Enums.AnimalStatus status, DateOnly? exitDate, string action, bool dateBased)
        : base(BuildMessage(tagId, status, exitDate, action, dateBased))
    {
        TagId = tagId;
        ExitDate = exitDate;
    }

    public string TagId { get; }
    public DateOnly? ExitDate { get; }

    private static string BuildMessage(string tagId, Enums.AnimalStatus status, DateOnly? exitDate, string action, bool dateBased)
    {
        var what = (status, exitDate) switch
        {
            (Enums.AnimalStatus.Sold, { } d) => $"was sold on {d:dd MMM yyyy}",
            (Enums.AnimalStatus.Dead, { } d) => $"died on {d:dd MMM yyyy}",
            _ => $"has left the farm ({status})"
        };

        return dateBased && exitDate is not null
            ? $"Cannot record {action} for animal '{tagId}': it {what}. Only records dated before {exitDate:dd MMM yyyy} can be added."
            : $"Cannot record {action} for animal '{tagId}': it {what}.";
    }
}

/// <summary>
/// Thrown when a sale/death date is before the animal's acquisition date or before its last
/// pen/shed placement (the movement history would otherwise end before it started).
/// HTTP 422.
/// </summary>
public sealed class InvalidExitDateException(string tagId, string reason)
    : LivestockDomainException($"Invalid exit date for animal '{tagId}': {reason}");

/// <summary>
/// Thrown when an animal is transferred into a pen whose on-farm occupancy already equals its
/// capacity. Sold/dead animals never count toward occupancy. HTTP 422.
/// </summary>
public sealed class PenFullException(string penName, int capacity)
    : LivestockDomainException($"Pen '{penName}' is full ({capacity} animals on farm). Move an animal out or raise the pen capacity first.");

/// <summary>
/// Thrown when a tape-measure length or heart girth is outside the plausible 20–120 inch range.
/// HTTP 422.
/// </summary>
public sealed class InvalidTapeMeasurementException(string message)
    : LivestockDomainException(message);
