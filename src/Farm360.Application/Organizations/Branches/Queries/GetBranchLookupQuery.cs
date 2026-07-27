using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Queries;

public record GetBranchLookupQuery(Guid? OrganizationId = null) : IRequest<IReadOnlyList<LookupDto>>;

internal sealed class GetBranchLookupQueryHandler : IRequestHandler<GetBranchLookupQuery, IReadOnlyList<LookupDto>>
{
    private readonly Farm360.Domain.Organizations.Repositories.IBranchRepository _repository;
    private readonly ITenantService _tenantService;

    public GetBranchLookupQueryHandler(Farm360.Domain.Organizations.Repositories.IBranchRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetBranchLookupQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetLookupsAsync(_tenantService.TenantId, request.OrganizationId, cancellationToken);
        return items.Select(i => new LookupDto(i.Id, i.Name, i.ParentId)).ToList();
    }
}
