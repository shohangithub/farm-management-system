using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.DiseaseIncidents;

public sealed record GetDiseaseIncidentListQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    IncidentStatus? Status = null,
    IncidentSeverity? Severity = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
) : IRequest<PagedResult<DiseaseIncidentDto>>;

internal sealed class GetDiseaseIncidentListQueryHandler(IDiseaseIncidentRepository repository)
    : IRequestHandler<GetDiseaseIncidentListQuery, PagedResult<DiseaseIncidentDto>>
{
    public async Task<PagedResult<DiseaseIncidentDto>> Handle(GetDiseaseIncidentListQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.Status,
            request.Severity,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(x => x.ToDto()).ToList();
        return new PagedResult<DiseaseIncidentDto>(dtos, count, pageNumber, pageSize);
    }
}
