using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using MediatR;

namespace Farm360.Application.Farms.Queries;

public record GetFarmLookupQuery(Guid? BranchId = null) : IRequest<IReadOnlyList<LookupDto>>;

internal sealed class GetFarmLookupQueryHandler : IRequestHandler<GetFarmLookupQuery, IReadOnlyList<LookupDto>>
{
    private readonly Farm360.Domain.Farms.Repositories.IFarmRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFarmLookupQueryHandler(Farm360.Domain.Farms.Repositories.IFarmRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetFarmLookupQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetLookupsAsync(_tenantService.TenantId, request.BranchId, cancellationToken);
        return items.Select(i => new LookupDto(i.Id, i.Name, i.ParentId)).ToList();
    }
}
