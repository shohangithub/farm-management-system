using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Queries;

public sealed record GetFarmByIdQuery(Guid Id) : IRequest<FarmDto>;

public sealed class GetFarmByIdQueryHandler : IRequestHandler<GetFarmByIdQuery, FarmDto>
{
    private readonly IFarmRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFarmByIdQueryHandler(IFarmRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<FarmDto> Handle(GetFarmByIdQuery request, CancellationToken cancellationToken)
    {
        var farm = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Farms.Farm), request.Id);

        return farm.ToDto();
    }
}
