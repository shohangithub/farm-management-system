using Farm360.Application.Common.Interfaces;
using Farm360.Application.Organizations.Branches.DTOs;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Queries;

public sealed record GetBranchesByOrganizationQuery(Guid OrganizationId) : IRequest<IReadOnlyList<BranchListDto>>;

public sealed class GetBranchesByOrganizationQueryHandler : IRequestHandler<GetBranchesByOrganizationQuery, IReadOnlyList<BranchListDto>>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;

    public GetBranchesByOrganizationQueryHandler(IBranchRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<BranchListDto>> Handle(GetBranchesByOrganizationQuery request, CancellationToken cancellationToken)
    {
        var branches = await _repository.GetAllByOrganizationAsync(_tenantService.TenantId, request.OrganizationId, cancellationToken);
        return branches.Select(b => b.ToListDto()).ToList();
    }
}
