using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class StockWriteOffFinanceEventHandler : INotificationHandler<StockWriteOffEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StockWriteOffFinanceEventHandler> _logger;

    public StockWriteOffFinanceEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<StockWriteOffFinanceEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(StockWriteOffEvent notification, CancellationToken cancellationToken)
    {
        if (notification.WriteOffQuantity <= 0 || notification.UnitCostBdt <= 0)
            return;

        var totalLossBdt = Math.Round(notification.WriteOffQuantity * notification.UnitCostBdt, 2);
        if (totalLossBdt <= 0)
            return;

        var transaction = FinancialTransaction.Create(
            tenantId: notification.TenantId,
            farmId: notification.FarmId,
            type: TransactionType.Expense,
            category: TransactionCategory.MiscellaneousExpense,
            amountBdt: totalLossBdt,
            transactionDate: DateTime.UtcNow,
            referenceId: notification.TransactionId.ToString(),
            notes: $"Inventory write-off ({notification.Reason}): {notification.WriteOffQuantity:0.##} units @ {notification.UnitCostBdt:0.##} BDT",
            description: $"Stock Write-Off - {notification.Reason}",
            isAutomated: true,
            sourceModule: "Inventory"
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted automated inventory write-off expense of {Amount} BDT for Item {ItemId}", totalLossBdt, notification.ItemId);
        }
    }
}
