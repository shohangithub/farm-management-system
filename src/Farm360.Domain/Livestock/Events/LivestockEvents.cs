using Farm360.Domain.Common;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock.Events;

/// <summary>
/// Raised when a new animal is successfully registered on the platform.
/// Subscribers: audit log writer, subscription limit updater.
/// </summary>
public sealed record AnimalRegisteredEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid FarmId,
    string TagId,
    AnimalSpecies Species,
    string BreedName,
    decimal? AcquisitionPriceBdt) : IDomainEvent;

/// <summary>
/// Raised when an animal is sold.
/// Subscribers: finance module (auto-posts income entry), audit log.
/// </summary>
public sealed record AnimalSoldEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid SoldBy,
    decimal SalePriceBdt,
    DateOnly SaleDate,
    string? BuyerName,
    decimal? SaleWeightKg) : IDomainEvent;

/// <summary>
/// Raised when an animal is recorded as dead.
/// Subscribers: finance module (posts mortality loss), audit log, health module.
/// </summary>
public sealed record AnimalDiedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    DisposalReason Cause,
    DateOnly DeathDate) : IDomainEvent;

/// <summary>
/// Raised when an animal is transferred to another shed or farm.
/// Subscribers: audit log.
/// </summary>
public sealed record AnimalTransferredEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid? FromShedId,
    Guid? ToShedId,
    DateOnly TransferDate) : IDomainEvent;

/// <summary>
/// Raised when an animal is placed under quarantine.
/// Subscribers: health module dashboard (alerts), audit log.
/// </summary>
public sealed record AnimalQuarantinedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    string Reason) : IDomainEvent;

/// <summary>
/// Raised when a weight record is logged.
/// Subscribers: ADG calculation service, audit log.
/// </summary>
public sealed record WeightRecordedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    decimal WeightKg,
    DateOnly RecordedDate) : IDomainEvent;

/// <summary>
/// Raised when a mating event is recorded.
/// </summary>
public sealed record MatingRecordedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid BreedingRecordId,
    DateOnly MatingDate,
    bool IsArtificialInsemination) : IDomainEvent;

/// <summary>
/// Raised when pregnancy is confirmed.
/// </summary>
public sealed record PregnancyConfirmedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid BreedingRecordId,
    DateOnly ConfirmDate,
    DateOnly ExpectedCalvingDate) : IDomainEvent;

/// <summary>
/// Raised when calving (birth) is recorded.
/// </summary>
public sealed record CalvingRecordedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid BreedingRecordId,
    DateOnly CalvingDate,
    string Outcome,
    int CalvesCount) : IDomainEvent;

public sealed record BcsRecordedEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    decimal Score,
    DateOnly RecordedDate) : IDomainEvent;

public sealed record AnimalAssignedToBatchEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid AnimalId,
    Guid TenantId,
    Guid? BatchId) : IDomainEvent;
