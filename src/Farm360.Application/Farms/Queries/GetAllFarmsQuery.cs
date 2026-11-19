using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Queries;

public sealed record GetAllFarmsQuery() : IRequest<IReadOnlyList<FarmListDto>>;

public sealed class GetAllFarmsQueryHandler : IRequestHandler<GetAllFarmsQuery, IReadOnlyList<FarmListDto>>
{
    private readonly IFarmRepository _repository;
    private readonly ITenantService _tenantService;

    public GetAllFarmsQueryHandler(IFarmRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FarmListDto>> Handle(GetAllFarmsQuery request, CancellationToken cancellationToken)
    {
        var farms = await _repository.GetAllByTenantAsync(_tenantService.TenantId, cancellationToken);
        // Head count = animals on the farm now (sold/dead excluded), not the stored column.
        var occupancies = await _repository.GetOccupancyByTenantAsync(_tenantService.TenantId, cancellationToken);
        return farms.Select(f => f.ToListDto() with
        {
            CurrentAnimalCount = occupancies.TryGetValue(f.Id, out var count) ? count : 0
        }).ToList().AsReadOnly();
    }
}
