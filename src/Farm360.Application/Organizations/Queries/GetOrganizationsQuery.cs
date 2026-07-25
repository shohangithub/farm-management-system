using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Application.Organizations.DTOs;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Queries;

public record GetOrganizationsQuery(
    string? SearchTerm = null,
    int? Status = null,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResult<OrganizationDto>>;

internal sealed class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, PagedResult<OrganizationDto>>
{
    private readonly IOrganizationRepository _repository;
    private readonly ITenantService _tenantService;

    public GetOrganizationsQueryHandler(IOrganizationRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<PagedResult<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _repository.GetPagedByTenantAsync(
            _tenantService.TenantId,
            request.SearchTerm,
            request.Status,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(o => o.ToDto()).ToList();

        return new PagedResult<OrganizationDto>(dtos, totalCount, request.PageNumber, request.PageSize);
    }
}
