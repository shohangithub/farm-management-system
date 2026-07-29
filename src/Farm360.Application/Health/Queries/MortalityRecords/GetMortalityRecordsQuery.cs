using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
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

    public GetMortalityRecordsQueryHandler(IMortalityRecordRepository repository)
    {
        _repository = repository;
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

        var dtos = items.Select(m => m.ToDto()).ToList();
        return new PagedResult<MortalityRecordDto>(dtos, count, pageNumber, pageSize);
    }
}
