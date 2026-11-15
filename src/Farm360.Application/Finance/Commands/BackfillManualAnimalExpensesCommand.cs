using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Internal;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance.Interfaces;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Finance.Commands;

/// <summary>
/// One-off repair: posts every hand-entered expense that is tagged to an animal onto that animal's
/// cost ledger, for expenses recorded before such postings reached the ledger at all.
/// </summary>
/// <remarks>
/// Safe to re-run. Vet/medicine expenses go through <see cref="AnimalCostLedgerUpdater.SyncManualVetAsync"/>,
/// which records on the transaction what it posted, so an already-posted expense is left alone.
/// Labour/overhead expenses get their single 100 % allocation row only if they have none, and the
/// buckets are then recomputed from the allocation table rather than incremented.
/// Transactional: either the whole farm is repaired or nothing changes.
/// </remarks>
public sealed record BackfillManualAnimalExpensesCommand(Guid FarmId)
    : IRequest<ManualAnimalExpenseBackfillResult>, ITransactionalCommand;

public sealed record ManualAnimalExpenseBackfillResult(
    int ExpensesConsidered,
    int VetExpensesPosted,
    decimal VetAmountPostedBdt,
    int AllocationRowsWritten,
    int LabourOverheadLedgersRefreshed);

public sealed class BackfillManualAnimalExpensesCommandValidator : AbstractValidator<BackfillManualAnimalExpensesCommand>
{
    public BackfillManualAnimalExpensesCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
    }
}

public sealed class BackfillManualAnimalExpensesCommandHandler
    : IRequestHandler<BackfillManualAnimalExpensesCommand, ManualAnimalExpenseBackfillResult>
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalOverheadAllocationRepository _allocations;
    private readonly IUnitOfWork _unitOfWork;

    public BackfillManualAnimalExpensesCommandHandler(
        IFinancialTransactionRepository transactions,
        IAnimalCostLedgerRepository ledgers,
        IAnimalOverheadAllocationRepository allocations,
        IUnitOfWork unitOfWork)
    {
        _transactions = transactions;
        _ledgers = ledgers;
        _allocations = allocations;
        _unitOfWork = unitOfWork;
    }

    public async Task<ManualAnimalExpenseBackfillResult> Handle(
        BackfillManualAnimalExpensesCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var expenses = (await _transactions.GetAllByFarmIdAsync(request.FarmId, cancellationToken).ConfigureAwait(false))
            .Where(AnimalCostLedgerUpdater.IsManualAnimalExpense)
            .OrderBy(t => t.TransactionDate)
            .ToList();

        var vetPosted = 0;
        var vetAmount = 0m;
        var rowsWritten = 0;
        var labourAnimals = new HashSet<Guid>();

        foreach (var expense in expenses)
        {
            if (AnimalCostLedgerUpdater.IsVetCategory(expense.Category))
            {
                var amountBefore = expense.LedgerVetPostedBdt ?? 0m;
                if (await AnimalCostLedgerUpdater.SyncManualVetAsync(_ledgers, expense, cancellationToken).ConfigureAwait(false))
                {
                    vetPosted++;
                    vetAmount += (expense.LedgerVetPostedBdt ?? 0m) - amountBefore;
                }
                await _transactions.UpdateAsync(expense, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (AnimalCostLedgerUpdater.BuildDirectAllocation(expense) is not { } row)
                continue; // Not an animal cost (e.g. interest).

            var existing = await _allocations.GetBySourceTransactionAsync(expense.Id, cancellationToken).ConfigureAwait(false);
            if (existing.Count == 0)
            {
                _allocations.AddRange([row]);
                rowsWritten++;
            }
            labourAnimals.Add(row.AnimalId);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Labour/overhead buckets are always recomputed from the allocation table.
        var refreshed = 0;
        foreach (var animalId in labourAnimals)
        {
            var ledger = await _ledgers.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
            if (ledger is null)
                continue;

            var totals = await _allocations.GetTotalsForAnimalAsync(animalId, cancellationToken).ConfigureAwait(false);
            ledger.UpdateLaborCost(totals.LabourBdt);
            ledger.UpdateOverheadCost(totals.OverheadBdt);
            _ledgers.Update(ledger);
            refreshed++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ManualAnimalExpenseBackfillResult(
            expenses.Count, vetPosted, Math.Round(vetAmount, 2), rowsWritten, refreshed);
    }
}
