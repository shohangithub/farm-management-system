using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Livestock.DTOs;

/// <summary>
/// Full animal detail DTO — returned by GetAnimalByIdQuery.
/// Includes all child records.
/// </summary>
public sealed record AnimalDto(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    Guid? BatchId,
    Guid? ShedId,
    Guid? PenId,
    string TagId,
    TagType TagType,
    AnimalSpecies Species,
    Guid BreedId,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    AcquisitionType AcquisitionType,
    DateOnly AcquisitionDate,
    decimal? AcquisitionPriceBdt,
    decimal? SalePriceBdt,
    DateOnly? SaleDate,
    string? BuyerName,
    decimal? SaleWeightKg,
    AnimalStatus Status,
    string? QuarantineReason,
    DisposalReason? DisposalReason,
    string? Notes,
    decimal? LatestWeightKg,
    DateOnly? LatestWeightDate,
    decimal? AdgKgPerDay,
    decimal? LatestBcs,
    string? PrimaryPhotoUrl,
    IReadOnlyList<WeightRecordDto> WeightRecords,
    IReadOnlyList<BreedingRecordDto> BreedingRecords,
    IReadOnlyList<AnimalPhotoDto> Photos,
    IReadOnlyList<AnimalMovementDto> Movements,
    IReadOnlyList<BcsRecordDto> BcsRecords,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    DateTime? ModifiedAtUtc);

/// <summary>
/// Lightweight list item DTO — returned by GetAnimalListQuery.
/// Excludes child collections for performance.
/// </summary>
public sealed record AnimalListItemDto(
    Guid Id,
    string TagId,
    TagType TagType,
    AnimalSpecies Species,
    Guid BreedId,
    AnimalSex Sex,
    DateOnly DateOfBirth,
    AnimalStatus Status,
    Guid FarmId,
    Guid? BatchId,
    Guid? ShedId,
    Guid? PenId,
    decimal? LatestWeightKg,
    DateOnly? LatestWeightDate,
    decimal? AdgKgPerDay,
    decimal? LatestBcs,
    string? PrimaryPhotoUrl,
    DateTime CreatedAtUtc);

/// <summary>
/// Weight record DTO.
/// </summary>
public sealed record WeightRecordDto(
    Guid Id,
    Guid AnimalId,
    decimal WeightKg,
    DateOnly RecordedDate,
    string? Notes,
    DateTime RecordedAtUtc);

/// <summary>
/// Body condition score DTO.
/// </summary>
public sealed record BcsRecordDto(
    Guid Id,
    Guid AnimalId,
    decimal Score,
    DateOnly RecordedDate,
    Guid EvaluatorId,
    string? Notes);

/// <summary>
/// Breeding record DTO.
/// </summary>
public sealed record BreedingRecordDto(
    Guid Id,
    Guid AnimalId,
    DateOnly MatingDate,
    Guid? SireAnimalId,
    string? SireExternalId,
    bool IsArtificialInsemination,
    DateOnly? PregnancyConfirmDate,
    bool IsPregnancyConfirmed,
    DateOnly? ExpectedCalvingDate,
    DateOnly? ActualCalvingDate,
    string? CalvingOutcome,
    int? CalvesCount,
    DateTime CreatedAtUtc);

/// <summary>
/// Animal photo DTO.
/// </summary>
public sealed record AnimalPhotoDto(
    Guid Id,
    Guid AnimalId,
    string PhotoUrl,
    string? Caption,
    bool IsPrimary,
    DateTime UploadedAtUtc);

/// <summary>
/// Animal movement history DTO.
/// </summary>
public sealed record AnimalMovementDto(
    Guid Id,
    Guid AnimalId,
    Guid? ShedId,
    Guid? PenId,
    DateTime PlacedAtUtc,
    Guid PlacedBy,
    DateTime? RemovedAtUtc,
    Guid? RemovedBy,
    string? TransferReason);

/// <summary>
/// Paginated list result wrapper used by GetAnimalListQuery.
/// </summary>
public sealed record PagedAnimalListDto(
    IReadOnlyList<AnimalListItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
