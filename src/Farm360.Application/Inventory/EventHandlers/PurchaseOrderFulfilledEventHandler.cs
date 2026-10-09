using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record PurchaseOrderFulfilledNotification(PurchaseOrderFulfilledEvent DomainEvent) : INotification;

public class PurchaseOrderFulfilledEventHandler : INotificationHandler<PurchaseOrderFulfilledNotification>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _stockTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PurchaseOrderFulfilledEventHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository stockTransactionRepository,
        IUnitOfWork unitOfWork)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _stockTransactionRepository = stockTransactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(PurchaseOrderFulfilledNotification notification, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderRepository.GetByIdWithItemsAsync(notification.DomainEvent.PurchaseOrderId, cancellationToken);

        if (purchaseOrder == null)
            return;

        foreach (var item in purchaseOrder.Items)
        {
            var inventoryItem = await _inventoryItemRepository.GetByIdAsync(item.InventoryItemId, cancellationToken);

            if (inventoryItem != null)
            {
                var itemTxId = Guid.NewGuid();
                // Receiving stock updates the InventoryItem aggregate and creates a StockTransaction entity
                inventoryItem.ReceiveStock(item.Quantity, item.UnitCostBdt, itemTxId);
                _inventoryItemRepository.Update(inventoryItem);

                var stockTransaction = new Farm360.Domain.Inventory.StockTransaction(
                    id: itemTxId,
                    tenantId: purchaseOrder.TenantId,
                    farmId: purchaseOrder.FarmId,
                    inventoryItemId: item.InventoryItemId,
                    transactionType: Farm360.Domain.Inventory.Enums.StockTransactionType.StockIn,
                    quantity: item.Quantity,
                    unitCostBdt: item.UnitCostBdt,
                    balanceAfter: inventoryItem.CurrentStock,
                    transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                    supplierId: purchaseOrder.SupplierId,
                    invoiceNumber: purchaseOrder.PoNumber,
                    reason: $"Received from Purchase Order {purchaseOrder.PoNumber}",
                    referenceId: purchaseOrder.Id);

                await _stockTransactionRepository.AddAsync(stockTransaction, cancellationToken);
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
