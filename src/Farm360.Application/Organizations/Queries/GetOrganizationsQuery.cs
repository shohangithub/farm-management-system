using Farm360.Application.Common.Interfaces;
using Farm360.Application.Organizations.DTOs;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Queries;

public record GetOrganizationsQuery : IRequest<IReadOnlyList<OrganizationDto>>;

internal sealed class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly IOrganizationRepository _repository;
    private readonly ITenantService _tenantService;

    public GetOrganizationsQueryHandler(IOrganizationRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var organizations = await _repository.GetAllByTenantAsync(_tenantService.TenantId, cancellationToken);
        return organizations.Select(o => o.ToDto()).ToList();
    }
}
