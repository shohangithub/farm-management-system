using Farm360.Application.Common.Interfaces;
using Farm360.Application.Organizations.Branches.DTOs;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Queries;

public sealed record GetBranchByIdQuery(Guid Id) : IRequest<BranchDto>;

public sealed class GetBranchByIdQueryHandler : IRequestHandler<GetBranchByIdQuery, BranchDto>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;

    public GetBranchByIdQueryHandler(IBranchRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<BranchDto> Handle(GetBranchByIdQuery request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Organizations.Branch), request.Id);

        return branch.ToDto();
    }
}
