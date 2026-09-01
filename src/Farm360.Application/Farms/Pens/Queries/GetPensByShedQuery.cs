using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Pens.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Pens.Queries;

public record GetPensByShedQuery(Guid ShedId) : IRequest<IReadOnlyList<PenListDto>>;

public class GetPensByShedQueryHandler : IRequestHandler<GetPensByShedQuery, IReadOnlyList<PenListDto>>
{
    private readonly IPenRepository _repository;
    private readonly ITenantService _tenantService;

    public GetPensByShedQueryHandler(IPenRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<PenListDto>> Handle(GetPensByShedQuery request, CancellationToken cancellationToken)
    {
        var pens = await _repository.GetAllByShedAsync(_tenantService.TenantId, request.ShedId, cancellationToken);
        var occupancies = await _repository.GetOccupancyByShedAsync(_tenantService.TenantId, request.ShedId, cancellationToken);

        return pens.Select(p => p.ToListDto() with { 
            CurrentOccupancy = occupancies.TryGetValue(p.Id, out var count) ? count : 0 
        }).ToList();
    }
}
