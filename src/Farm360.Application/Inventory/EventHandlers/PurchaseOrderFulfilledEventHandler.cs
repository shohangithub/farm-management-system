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
    private readonly IUnitOfWork _unitOfWork;

    public PurchaseOrderFulfilledEventHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUnitOfWork unitOfWork)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(PurchaseOrderFulfilledNotification notification, CancellationToken cancellationToken)
    {
        var purchaseOrder = await _purchaseOrderRepository.GetByIdWithItemsAsync(notification.DomainEvent.PurchaseOrderId, cancellationToken);

        if (purchaseOrder == null)
            return;

        var transactionId = Guid.NewGuid(); // One transaction ID for all items received in this PO

        foreach (var item in purchaseOrder.Items)
        {
            var inventoryItem = await _inventoryItemRepository.GetByIdAsync(item.InventoryItemId, cancellationToken);

            if (inventoryItem != null)
            {
                // Receiving stock updates the InventoryItem aggregate and creates a StockTransaction entity
                inventoryItem.ReceiveStock(item.Quantity, item.UnitCostBdt, transactionId);
                _inventoryItemRepository.Update(inventoryItem);
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
