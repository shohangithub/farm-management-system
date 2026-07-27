using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.MortalityRecords;

public sealed record GetMortalityRecordsQuery(
    int PageNumber = 1,
    int PageSize = 10
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
        var (items, count) = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(m => m.ToDto()).ToList();
        return new PagedResult<MortalityRecordDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
