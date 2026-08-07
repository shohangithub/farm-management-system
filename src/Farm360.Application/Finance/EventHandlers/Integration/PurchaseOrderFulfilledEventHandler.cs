using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public class PurchaseOrderFulfilledEventHandler : INotificationHandler<PurchaseOrderFulfilledNotification>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IFinancialTransactionRepository _financialTransactionRepository;
    private readonly ILogger<PurchaseOrderFulfilledEventHandler> _logger;

    public PurchaseOrderFulfilledEventHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IFinancialTransactionRepository financialTransactionRepository,
        ILogger<PurchaseOrderFulfilledEventHandler> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _financialTransactionRepository = financialTransactionRepository;
        _logger = logger;
    }

    public async Task Handle(PurchaseOrderFulfilledNotification notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var eventName = notification.DomainEvent.GetType().Name;
            _logger.LogInformation("Domain Event: {DomainEvent} triggered in Finance module.", eventName);
        }

        var poId = notification.DomainEvent.PurchaseOrderId;
        var po = await _purchaseOrderRepository.GetByIdAsync(poId, cancellationToken);
        if (po == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Purchase Order {PurchaseOrderId} not found. Cannot post financial expense.", poId);
            }
            return;
        }

        // Create an expense transaction for the inventory purchase
        var expense = FinancialTransaction.Create(
            po.TenantId,
            po.FarmId,
            TransactionType.Expense,
            TransactionCategory.InventoryPurchase,
            po.TotalAmountBdt,
            DateTime.UtcNow,
            referenceId: po.PoNumber,
            notes: $"Auto-generated expense from Purchase Order {po.PoNumber}"
        );

        await _financialTransactionRepository.AddAsync(expense, cancellationToken);
        
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted financial expense of {Amount} BDT for Purchase Order {PoNumber}.", po.TotalAmountBdt, po.PoNumber);
        }
    }
}
