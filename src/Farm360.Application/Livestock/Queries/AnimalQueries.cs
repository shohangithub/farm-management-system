using Farm360.Application.Common.Exceptions;
using Farm360.Application.Livestock.DTOs;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Livestock.Queries;

// ══════════════════════════════════════════════════════════════════════════════
// GET ANIMAL BY ID
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Returns full animal detail including all child collections.
/// Permission: animals:read
/// </summary>
public sealed record GetAnimalByIdQuery(Guid AnimalId) : IRequest<AnimalDto?>;

public sealed class GetAnimalByIdQueryHandler(IAnimalRepository repository)
    : IRequestHandler<GetAnimalByIdQuery, AnimalDto?>
{
    public async Task<AnimalDto?> Handle(GetAnimalByIdQuery request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdAsync(request.AnimalId, cancellationToken);
        return animal?.ToDto();
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// GET ANIMAL LIST (paged, filtered, sorted)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Returns paginated list of animals.
/// All filter parameters are optional.
/// Permission: animals:read
/// </summary>
public sealed record GetAnimalListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    Guid? PenId = null,
    AnimalSpecies? Species = null,
    AnimalSex? Sex = null,
    AnimalStatus? Status = null,
    string? SearchTerm = null,
    string? SortBy = null,
    bool SortDescending = false) : IRequest<PagedAnimalListDto>;

public sealed class GetAnimalListQueryHandler(IAnimalRepository repository)
    : IRequestHandler<GetAnimalListQuery, PagedAnimalListDto>
{
    public async Task<PagedAnimalListDto> Handle(GetAnimalListQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, totalCount) = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.BatchId,
            request.ShedId,
            request.PenId,
            request.Species,
            request.Sex,
            request.Status,
            request.SearchTerm,
            request.SortBy,
            request.SortDescending,
            cancellationToken);

        return new PagedAnimalListDto(
            items.Select(a => a.ToListItemDto()).ToList().AsReadOnly(),
            totalCount,
            pageNumber,
            pageSize);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// GET WEIGHT HISTORY
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Returns the full weight history for a single animal, ordered chronologically.
/// Permission: animals:read
/// </summary>
public sealed record GetAnimalWeightHistoryQuery(Guid AnimalId) : IRequest<IReadOnlyList<WeightRecordDto>>;

public sealed class GetAnimalWeightHistoryQueryHandler(IAnimalRepository repository)
    : IRequestHandler<GetAnimalWeightHistoryQuery, IReadOnlyList<WeightRecordDto>>
{
    public async Task<IReadOnlyList<WeightRecordDto>> Handle(
        GetAnimalWeightHistoryQuery request, CancellationToken cancellationToken)
    {
        var animal = await repository.GetByIdWithWeightsAsync(request.AnimalId, cancellationToken)
            ?? throw new NotFoundException(nameof(Animal), request.AnimalId);

        return animal.WeightRecords
            .OrderBy(w => w.RecordedDate)
            .Select(w => w.ToDto())
            .ToList()
            .AsReadOnly();
    }
}
