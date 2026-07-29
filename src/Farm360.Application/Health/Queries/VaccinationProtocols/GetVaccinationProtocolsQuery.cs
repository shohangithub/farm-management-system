using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VaccinationProtocols;

public sealed record GetVaccinationProtocolsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false
) : IRequest<PagedResult<VaccinationProtocolDto>>;

internal sealed class GetVaccinationProtocolsQueryHandler : IRequestHandler<GetVaccinationProtocolsQuery, PagedResult<VaccinationProtocolDto>>
{
    private readonly IVaccinationRepository _repository;

    public GetVaccinationProtocolsQueryHandler(IVaccinationRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<VaccinationProtocolDto>> Handle(GetVaccinationProtocolsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedProtocolsAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(p => p.ToDto()).ToList();
        return new PagedResult<VaccinationProtocolDto>(dtos, count, pageNumber, pageSize);
    }
}
