using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class DailyEntryConfirmedFinanceEventHandler : INotificationHandler<DailyEntryConfirmedEvent>
{
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyEntryConfirmedFinanceEventHandler> _logger;

    public DailyEntryConfirmedFinanceEventHandler(
        IDailyFeedingEntryRepository entryRepository,
        IAnimalFeedingPlanRepository planRepository,
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyEntryConfirmedFinanceEventHandler> logger)
    {
        _entryRepository = entryRepository;
        _planRepository = planRepository;
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyEntryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        var entry = await _entryRepository.GetByIdAsync(notification.EntryId, cancellationToken);
        if (entry == null)
            return;

        var cost = entry.TotalCostBdt ?? 0m;
        if (cost <= 0)
            return;

        Guid? animalId = null;
        if (entry.FeedingPlanId != Guid.Empty)
        {
            var plan = await _planRepository.GetByIdAsync(entry.FeedingPlanId, cancellationToken);
            animalId = plan?.AnimalId;
        }

        // 1. Post FeedCost expense to General Ledger
        var transaction = FinancialTransaction.Create(
            tenantId: notification.TenantId,
            farmId: notification.FarmId,
            type: TransactionType.Expense,
            category: TransactionCategory.FeedCost,
            amountBdt: cost,
            transactionDate: entry.EntryDate.ToDateTime(TimeOnly.MinValue),
            referenceId: entry.Id.ToString(),
            notes: $"Daily feeding consumption: {entry.ActualKg:0.##} kg",
            description: $"Feed Consumption on {entry.EntryDate}",
            animalId: animalId,
            batchId: entry.BatchId,
            shedId: entry.ShedId
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // 2. If single-animal plan, accumulate to that animal's cost ledger
        if (animalId.HasValue)
        {
            var ledger = await _ledgerRepository.GetByAnimalIdAsync(animalId.Value, cancellationToken);
            if (ledger == null)
            {
                ledger = AnimalCostLedger.Create(notification.TenantId, animalId.Value, notification.FarmId, 0m);
                ledger.RecordCost(TransactionCategory.FeedCost, cost);
                _ledgerRepository.Add(ledger);
            }
            else
            {
                ledger.RecordCost(TransactionCategory.FeedCost, cost);
                _ledgerRepository.Update(ledger);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted feed cost of {Cost} BDT for DailyFeedingEntry {EntryId}", cost, entry.Id);
        }
    }
}
