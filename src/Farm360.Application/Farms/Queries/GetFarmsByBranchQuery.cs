using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Queries;

public sealed record GetFarmsByBranchQuery(Guid BranchId) : IRequest<IReadOnlyList<FarmListDto>>;

public sealed class GetFarmsByBranchQueryHandler : IRequestHandler<GetFarmsByBranchQuery, IReadOnlyList<FarmListDto>>
{
    private readonly IFarmRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFarmsByBranchQueryHandler(IFarmRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<FarmListDto>> Handle(GetFarmsByBranchQuery request, CancellationToken cancellationToken)
    {
        var farms = await _repository.GetAllByBranchAsync(_tenantService.TenantId, request.BranchId, cancellationToken);
        var occupancies = await _repository.GetOccupancyByTenantAsync(_tenantService.TenantId, cancellationToken);

        return farms.Select(f => f.ToListDto() with { 
            CurrentAnimalCount = occupancies.TryGetValue(f.Id, out var count) ? count : 0 
        }).ToList();
    }
}
