using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Commands;

public record ActivateBranchCommand(Guid Id) : IRequest, ITransactionalCommand;

internal sealed class ActivateBranchCommandHandler : IRequestHandler<ActivateBranchCommand>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateBranchCommandHandler(IBranchRepository repository, ITenantService tenantService, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(ActivateBranchCommand request, CancellationToken cancellationToken)
    {
        var branch = await _repository.GetByIdAsync(_tenantService.TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Branch {request.Id} not found.");

        branch.ChangeStatus(Farm360.Domain.Organizations.Enums.BranchStatus.Active);
        
        _repository.Update(branch);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
