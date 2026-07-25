using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Organizations.Branches.DTOs;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Queries;

public sealed record GetBranchesByOrganizationQuery(
    Guid OrganizationId,
    string? SearchTerm = null,
    int? Status = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResult<BranchListDto>>;

public sealed class GetBranchesByOrganizationQueryHandler : IRequestHandler<GetBranchesByOrganizationQuery, PagedResult<BranchListDto>>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;

    public GetBranchesByOrganizationQueryHandler(IBranchRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<PagedResult<BranchListDto>> Handle(GetBranchesByOrganizationQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedByOrganizationAsync(
            _tenantService.TenantId, 
            request.OrganizationId,
            request.SearchTerm,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);
            
        var dtos = items.Select(b => b.ToListDto()).ToList();
        return new PagedResult<BranchListDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
