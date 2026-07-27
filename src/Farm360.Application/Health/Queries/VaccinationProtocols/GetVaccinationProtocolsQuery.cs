using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Health.DTOs;
using Farm360.Application.Health.Mappings;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.VaccinationProtocols;

public sealed record GetVaccinationProtocolsQuery(
    int PageNumber = 1,
    int PageSize = 10,
    string? SearchTerm = null
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
        var (items, count) = await _repository.GetPagedProtocolsAsync(
            request.PageNumber,
            request.PageSize,
            request.SearchTerm,
            cancellationToken);

        var dtos = items.Select(p => p.ToDto()).ToList();
        return new PagedResult<VaccinationProtocolDto>(dtos, count, request.PageNumber, request.PageSize);
    }
}
