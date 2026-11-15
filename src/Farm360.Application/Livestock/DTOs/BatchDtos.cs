using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Livestock.DTOs;

/// <param name="AnimalCount">Animals of this batch currently on the farm (Active + Quarantined —
/// <c>AnimalStatusGroups.OnFarm</c>), consistent with every other head count.</param>
/// <param name="TotalEverInBatch">Every animal assigned to the batch, including sold, dead and
/// slaughtered ones (batch history).</param>
public sealed record BatchDto(
    Guid Id,
    Guid TenantId,
    Guid FarmId,
    string Name,
    BatchStatus Status,
    string? Notes,
    int AnimalCount,
    int TotalEverInBatch,
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
