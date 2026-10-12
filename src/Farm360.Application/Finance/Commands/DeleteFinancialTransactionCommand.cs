using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance;
using MediatR;

namespace Farm360.Application.Finance.Commands;

/// <summary>Transactional: the delete and the removal of its animal allocations commit together.</summary>
public sealed record DeleteFinancialTransactionCommand(Guid Id, Guid FarmId) : IRequest, ITransactionalCommand;

public sealed class DeleteFinancialTransactionCommandHandler : IRequestHandler<DeleteFinancialTransactionCommand>
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly IOverheadAllocationService _overheadAllocation;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteFinancialTransactionCommandHandler(
        IFinancialTransactionRepository repository,
        IOverheadAllocationService overheadAllocation,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _overheadAllocation = overheadAllocation;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

        await _repository.DeleteAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A shared expense already split onto animals would otherwise stay on their ledgers forever.
        await _overheadAllocation.ReallocateTransactionAsync(request.Id, cancellationToken);
    }
}
