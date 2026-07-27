using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Queries;

public record GetOrganizationLookupQuery() : IRequest<IReadOnlyList<LookupDto>>;

internal sealed class GetOrganizationLookupQueryHandler : IRequestHandler<GetOrganizationLookupQuery, IReadOnlyList<LookupDto>>
{
    private readonly IOrganizationRepository _repository;
    private readonly ITenantService _tenantService;

    public GetOrganizationLookupQueryHandler(IOrganizationRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<LookupDto>> Handle(GetOrganizationLookupQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetLookupsAsync(_tenantService.TenantId, cancellationToken);
        return items.Select(i => new LookupDto(i.Id, i.Name, i.ParentId)).ToList();
    }
}
