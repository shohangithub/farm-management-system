using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VetVisits;

public sealed record GetVetVisitListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
) : IRequest<PagedResult<VetVisitDto>>;

internal sealed class GetVetVisitListQueryHandler : IRequestHandler<GetVetVisitListQuery, PagedResult<VetVisitDto>>
{
    private readonly IVetVisitRepository _repository;

    public GetVetVisitListQueryHandler(IVetVisitRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<VetVisitDto>> Handle(GetVetVisitListQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(v => v.ToDto()).ToList();
        return new PagedResult<VetVisitDto>(dtos, count, pageNumber, pageSize);
    }
}
