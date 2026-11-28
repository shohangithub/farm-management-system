using Farm360.Application.Finance.EventHandlers.Internal;
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
    private readonly Farm360.Domain.Finance.Interfaces.ITradeBillRepository _bills;
    private readonly Farm360.Domain.Finance.Interfaces.IAnimalCostLedgerRepository _ledgers;
    private readonly IMoneyAccountService? _money;

    public DeleteFinancialTransactionCommandHandler(
        IFinancialTransactionRepository repository,
        IOverheadAllocationService overheadAllocation,
        IUnitOfWork unitOfWork,
        Farm360.Domain.Finance.Interfaces.ITradeBillRepository bills,
        Farm360.Domain.Finance.Interfaces.IAnimalCostLedgerRepository ledgers,
        IMoneyAccountService? money = null)
    {
        _money = money;
        _ledgers = ledgers;
        _repository = repository;
        _overheadAllocation = overheadAllocation;
        _unitOfWork = unitOfWork;
        _bills = bills;
    }

    public async Task Handle(DeleteFinancialTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(FinancialTransaction), request.Id);

        AccountChart.EnsureEditable(transaction);

        // An unpaid bill goes with its entry; a bill with payments keeps the entry alive.
        if (transaction.SourceDocumentId is { } sourceId
            && await _bills.FindBySourceAsync(transaction.FarmId, sourceId, transaction.Category, cancellationToken) is { } bill)
        {
            if (bill.HasSettlements)
                throw new InvalidOperationException("Payments have been recorded against this entry's bill. Void them in Payables & Receivables before deleting it.");
            bill.Cancel();
            _bills.Update(bill);
        }

        // A hand-entered vet expense was added straight into the animal's vet bucket: take it back out.
        await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, transaction, cancellationToken, removed: true);

        // Paid directly by a partner: the capital contribution booked for it goes too.
        if (_money is not null)
            await _money.RemoveLinkedAsync(transaction, cancellationToken);

        await _repository.DeleteAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // A shared expense already split onto animals would otherwise stay on their ledgers forever.
        await _overheadAllocation.ReallocateTransactionAsync(request.Id, cancellationToken);
    }
}
