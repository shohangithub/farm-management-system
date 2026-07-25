using Farm360.Domain.Farms.Enums;

namespace Farm360.Application.Farms.Sheds.DTOs;

public sealed record ShedDto(
    Guid Id,
    Guid FarmId,
    string ShedNumber,
    string ShedName,
    int? Capacity,
    int CurrentOccupancy,
    string? AnimalType,
    string? FloorType,
    string? RoofType,
    bool HasVentilation,
    bool HasWaterLine,
    bool HasFeedLine,
    ShedStatus Status,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    DateTime? ModifiedAtUtc,
    Guid? ModifiedBy);

public sealed record ShedListDto(
    Guid Id,
    string ShedNumber,
    string ShedName,
    int? Capacity,
    int CurrentOccupancy,
    string? AnimalType,
    ShedStatus Status);
