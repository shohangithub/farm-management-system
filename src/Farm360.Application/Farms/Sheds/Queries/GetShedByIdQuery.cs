using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Sheds.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Sheds.Queries;

public sealed record GetShedByIdQuery(Guid Id) : IRequest<ShedDto>;

public sealed class GetShedByIdQueryHandler : IRequestHandler<GetShedByIdQuery, ShedDto>
{
    private readonly IShedRepository _repository;
    private readonly ITenantService _tenantService;

    public GetShedByIdQueryHandler(IShedRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<ShedDto> Handle(GetShedByIdQuery request, CancellationToken cancellationToken)
    {
        var shed = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Shed), request.Id);

        var occupancy = await _repository.GetOccupancyByShedAsync(_tenantService.TenantId, request.Id, cancellationToken);
        return shed.ToDto() with { CurrentOccupancy = occupancy };
    }
}
