using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Organizations.Branches.Commands;

public sealed record DeleteBranchCommand(Guid Id) : IRequest;

public sealed class DeleteBranchCommandHandler : IRequestHandler<DeleteBranchCommand>
{
    private readonly IBranchRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteBranchCommandHandler(
        IBranchRepository repository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteBranchCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var branch = await _repository.GetByIdAsync(tenantId, request.Id, cancellationToken)
            ?? throw new Farm360.Application.Common.Exceptions.NotFoundException(nameof(Domain.Organizations.Branch), request.Id);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _repository.Delete(branch);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(transaction, cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(transaction, cancellationToken);
            throw;
        }
    }
}
