using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class DailyEntryConfirmedFinanceEventHandler : INotificationHandler<DailyFeedingCostCalculatedEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyEntryConfirmedFinanceEventHandler> _logger;

    public DailyEntryConfirmedFinanceEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyEntryConfirmedFinanceEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyFeedingCostCalculatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TotalCostBdt <= 0)
            return;

        // 1. Post FeedCost expense to General Ledger
        var transaction = FinancialTransaction.Create(
            tenantId: notification.TenantId,
            farmId: notification.FarmId,
            type: TransactionType.Expense,
            category: TransactionCategory.FeedCost,
            amountBdt: notification.TotalCostBdt,
            transactionDate: notification.EntryDate.ToDateTime(TimeOnly.MinValue),
            referenceId: notification.EntryId.ToString(),
            notes: $"Daily feeding consumption: {notification.ActualKg:0.##} kg",
            description: $"Feed Consumption on {notification.EntryDate}",
            animalId: notification.AnimalId,
            batchId: notification.BatchId,
            shedId: notification.ShedId,
            isAutomated: true,
            sourceModule: "Feeding"
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // 2. If single-animal plan, accumulate to that animal's cost ledger
        if (notification.AnimalId.HasValue)
        {
            var ledger = await _ledgerRepository.GetByAnimalIdAsync(notification.AnimalId.Value, cancellationToken);
            if (ledger == null)
            {
                ledger = AnimalCostLedger.Create(notification.TenantId, notification.AnimalId.Value, notification.FarmId, 0m);
                ledger.RecordCost(TransactionCategory.FeedCost, notification.TotalCostBdt);
                _ledgerRepository.Add(ledger);
            }
            else
            {
                ledger.RecordCost(TransactionCategory.FeedCost, notification.TotalCostBdt);
                _ledgerRepository.Update(ledger);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted automated feed cost of {Cost} BDT for DailyFeedingEntry {EntryId}", notification.TotalCostBdt, notification.EntryId);
        }
    }
}
