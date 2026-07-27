using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.DiseaseIncidents;

public sealed record GetDiseaseIncidentListQuery(int PageNumber = 1, int PageSize = 10) : IRequest<PagedResult<DiseaseIncidentDto>>;

internal sealed class GetDiseaseIncidentListQueryHandler(IDiseaseIncidentRepository repository)
    : IRequestHandler<GetDiseaseIncidentListQuery, PagedResult<DiseaseIncidentDto>>
{
    public async Task<PagedResult<DiseaseIncidentDto>> Handle(GetDiseaseIncidentListQuery request, CancellationToken cancellationToken)
    {
        var (items, count) = await repository.GetPagedAsync(request.PageNumber, request.PageSize, cancellationToken);
        var dtos = items.Select(x => x.ToDto()).ToList();
        return new PagedResult<DiseaseIncidentDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
