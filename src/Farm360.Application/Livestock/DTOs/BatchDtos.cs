using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Livestock.DTOs;

public sealed record BatchDto(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    string Name,
    BatchStatus Status,
    string? Notes,
    int AnimalCount,
    DateTime CreatedAtUtc);

public sealed record PagedBatchListDto(
    IReadOnlyList<BatchDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
