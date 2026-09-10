using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class DailyConsumableCostCalculatedFinanceEventHandler : INotificationHandler<DailyConsumableCostCalculatedEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyConsumableCostCalculatedFinanceEventHandler> _logger;

    public DailyConsumableCostCalculatedFinanceEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyConsumableCostCalculatedFinanceEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyConsumableCostCalculatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TotalCostBdt <= 0)
            return;

        try
        {
            // Post ConsumableExpense transaction to General Ledger
            var transaction = FinancialTransaction.Create(
                tenantId: notification.TenantId,
                farmId: notification.FarmId,
                type: TransactionType.Expense,
                category: TransactionCategory.ConsumableExpense,
                amountBdt: notification.TotalCostBdt,
                transactionDate: notification.EntryDate.ToDateTime(TimeOnly.MinValue),
                referenceId: notification.EntryId.ToString(),
                notes: $"Daily consumable usage: {notification.ActualQuantity:0.##} ({notification.ItemName})",
                description: $"Daily Consumable Usage - {notification.ItemName}",
                isAutomated: true,
                sourceModule: "Inventory");

            await _transactionRepository.AddAsync(transaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Posted automated consumable expense of {Cost} BDT for DailyConsumableEntry {EntryId}", 
                    notification.TotalCostBdt, notification.EntryId);
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post automated financial transaction for daily consumable entry {EntryId}", notification.EntryId);
        }
#pragma warning restore CA1031
    }
}
