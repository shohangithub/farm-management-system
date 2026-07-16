using Farm360.Domain.Farms.Enums;

namespace Farm360.Application.Farms.DTOs;

public sealed record FarmDto(
    Guid Id,
    Guid BranchId,
    string FarmCode,
    string FarmName,
    FarmType Type,
    double? FarmSize,
    double? LandArea,
    double? Latitude,
    double? Longitude,
    string? MapPolygon,
    int? Capacity,
    int CurrentAnimalCount,
    string? OwnerId,
    string? ManagerId,
    FarmStatus Status,
    string? Description);

public sealed record FarmListDto(
    Guid Id,
    string FarmCode,
    string FarmName,
    FarmType Type,
    int CurrentAnimalCount,
    int? Capacity,
    FarmStatus Status);
