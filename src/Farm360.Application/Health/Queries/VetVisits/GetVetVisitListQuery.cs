using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VetVisits;

public sealed record GetVetVisitListQuery(
    Guid? FarmId = null,
    int PageNumber = 1,
    int PageSize = 10
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
        var (items, count) = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.FarmId,
            cancellationToken);

        var dtos = items.Select(v => v.ToDto()).ToList();
        return new PagedResult<VetVisitDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
