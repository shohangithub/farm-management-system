using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Application.Livestock.Queries;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.MortalityRecords;

public sealed record GetMortalityRecordsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? AnimalId = null,
    string? Reason = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
) : IRequest<PagedResult<MortalityRecordDto>>;

internal sealed class GetMortalityRecordsQueryHandler : IRequestHandler<GetMortalityRecordsQuery, PagedResult<MortalityRecordDto>>
{
    private readonly IMortalityRecordRepository _repository;
    private readonly IAnimalTagNames? _tags;

    public GetMortalityRecordsQueryHandler(IMortalityRecordRepository repository, IAnimalTagNames? tags = null)
    {
        _repository = repository;
        _tags = tags;
    }

    public async Task<PagedResult<MortalityRecordDto>> Handle(GetMortalityRecordsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.AnimalId,
            request.Reason,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        // Ear tags for the whole page in one lookup.
        var tags = await _tags.TagsForAsync(items.Select(m => m.AnimalId), cancellationToken);

        var dtos = items.Select(m => m.ToDto(tags.GetValueOrDefault(m.AnimalId))).ToList();
        return new PagedResult<MortalityRecordDto>(dtos, count, pageNumber, pageSize);
    }
}
