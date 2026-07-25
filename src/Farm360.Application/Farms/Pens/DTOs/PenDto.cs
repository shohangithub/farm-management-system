using Farm360.Domain.Farms;

namespace Farm360.Application.Farms.Pens.DTOs;

public record PenDto(
    Guid Id,
    Guid ShedId,
    string PenNumber,
    string PenName,
    int Capacity,
    int CurrentOccupancy,
    string? AnimalGroup,
    string? Notes,
    int Status,
    DateTime CreatedAtUtc,
    Guid CreatedBy,
    DateTime? ModifiedAtUtc,
    Guid? ModifiedBy);

public record PenListDto(
    Guid Id,
    string PenNumber,
    string PenName,
    int Capacity,
    int CurrentOccupancy,
    string? AnimalGroup,
    int Status);

public static class PenMappingExtensions
{
    public static PenDto ToDto(this Pen pen)
    {
        return new PenDto(
            pen.Id,
            pen.ShedId,
            pen.PenNumber,
            pen.PenName,
            pen.Capacity,
            pen.CurrentOccupancy,
            pen.AnimalGroup,
            pen.Notes,
            (int)pen.Status,
            pen.CreatedAtUtc,
            pen.CreatedBy,
            pen.ModifiedAtUtc,
            pen.ModifiedBy);
    }

    public static PenListDto ToListDto(this Pen pen)
    {
        return new PenListDto(
            pen.Id,
            pen.PenNumber,
            pen.PenName,
            pen.Capacity,
            pen.CurrentOccupancy,
            pen.AnimalGroup,
            (int)pen.Status);
    }
}
