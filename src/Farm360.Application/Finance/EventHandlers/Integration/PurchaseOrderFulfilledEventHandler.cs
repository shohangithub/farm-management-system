using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Books a received purchase order: Dr Inventory, Cr Cash. The goods are an asset now and become
/// an expense only when they are fed, used or written off — expensing them here as well as on use
/// was the old double count. Keyed on the purchase order, so it can never post twice.
/// </summary>
public class PurchaseOrderFulfilledEventHandler : INotificationHandler<PurchaseOrderFulfilledNotification>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PurchaseOrderFulfilledEventHandler> _logger;

    public PurchaseOrderFulfilledEventHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork,
        ILogger<PurchaseOrderFulfilledEventHandler> logger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(PurchaseOrderFulfilledNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var poId = notification.DomainEvent.PurchaseOrderId;

        // Items must be loaded: the order total is the sum of its lines.
        var po = await _purchaseOrderRepository.GetByIdWithItemsAsync(poId, cancellationToken);
        if (po == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Purchase Order {PurchaseOrderId} not found. Cannot post the stock purchase.", poId);
            return;
        }

        // Dated on receipt — the day the goods (and the payment) changed hands.
        var receivedOn = notification.DomainEvent.OccurredOnUtc.Date;

        await _ledgerPosting.PostAsync(new LedgerPosting(
            po.TenantId,
            po.FarmId,
            TransactionCategory.InventoryPurchase,
            po.TotalAmountBdt,
            receivedOn,
            po.Id,
            InventoryAccounting.Module,
            $"Stock purchase - PO {po.PoNumber}",
            Notes: $"Goods received against Purchase Order {po.PoNumber} ({po.Items.Count} line(s))",
            ReferenceId: po.PoNumber), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted stock purchase of {Amount} BDT for PO {PoNumber}.", po.TotalAmountBdt, po.PoNumber);
        }
    }
}
