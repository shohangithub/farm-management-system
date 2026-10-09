using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public class PurchaseReturnCompletedFinanceHandler : INotificationHandler<PurchaseReturnCompletedNotification>
{
    private readonly IFinancialTransactionRepository _financialTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PurchaseReturnCompletedFinanceHandler> _logger;

    public PurchaseReturnCompletedFinanceHandler(
        IFinancialTransactionRepository financialTransactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<PurchaseReturnCompletedFinanceHandler> logger)
    {
        _financialTransactionRepository = financialTransactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(PurchaseReturnCompletedNotification notification, CancellationToken cancellationToken)
    {
        var evt = notification.DomainEvent;
        if (evt.TotalAmountBdt <= 0)
            return;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Processing finance posting for Purchase Return {ReturnNumber} with amount {Amount} BDT", evt.ReturnNumber, evt.TotalAmountBdt);
        }

        var creditTransaction = FinancialTransaction.Create(
            tenantId: evt.TenantId,
            farmId: evt.FarmId,
            type: TransactionType.Income,
            category: TransactionCategory.InventoryPurchaseReturn,
            amountBdt: evt.TotalAmountBdt,
            transactionDate: evt.ReturnDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            referenceId: evt.ReturnNumber,
            notes: $"Supplier refund / credit for Purchase Return {evt.ReturnNumber}",
            description: $"Purchase Return - {evt.ReturnNumber}",
            isAutomated: true,
            sourceModule: "Inventory"
        );

        await _financialTransactionRepository.AddAsync(creditTransaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted financial credit of {Amount} BDT for Purchase Return {ReturnNumber}.", evt.TotalAmountBdt, evt.ReturnNumber);
        }
    }
}
