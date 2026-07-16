using Farm360.Application.Common.Interfaces;
using Farm360.Application.Farms.Sheds.DTOs;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Farms.Sheds.Queries;

public sealed record GetShedsByFarmQuery(Guid FarmId) : IRequest<IReadOnlyList<ShedListDto>>;

public sealed class GetShedsByFarmQueryHandler : IRequestHandler<GetShedsByFarmQuery, IReadOnlyList<ShedListDto>>
{
    private readonly IShedRepository _repository;
    private readonly ITenantService _tenantService;

    public GetShedsByFarmQueryHandler(IShedRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<ShedListDto>> Handle(GetShedsByFarmQuery request, CancellationToken cancellationToken)
    {
        var sheds = await _repository.GetAllByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
        return sheds.Select(s => s.ToListDto()).ToList();
    }
}
