using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseReturns;

public sealed record CompletePurchaseReturnCommand(Guid Id) : IRequest, ITransactionalCommand;

public sealed class CompletePurchaseReturnCommandHandler : IRequestHandler<CompletePurchaseReturnCommand>
{
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public CompletePurchaseReturnCommandHandler(
        IPurchaseReturnRepository returnRepository,
        IPurchaseOrderRepository poRepository,
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository,
        ICurrentUserService currentUserService,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _returnRepository = returnRepository;
        _poRepository = poRepository;
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
        _currentUserService = currentUserService;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
    }

    public async Task Handle(CompletePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var purchaseReturn = await _returnRepository.GetByIdWithItemsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("PurchaseReturn", request.Id);

        if (purchaseReturn.Status != PurchaseReturnStatus.Draft)
            throw new InventoryDomainException($"Cannot complete purchase return in '{purchaseReturn.Status}' status. Only draft returns can be completed.");

        var po = await _poRepository.GetByIdWithItemsAsync(purchaseReturn.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("PurchaseOrder", purchaseReturn.PurchaseOrderId);

        foreach (var returnItem in purchaseReturn.Items)
        {
            var inventoryItem = await _itemRepository.GetByIdAsync(returnItem.InventoryItemId, cancellationToken)
                ?? throw new NotFoundException("InventoryItem", returnItem.InventoryItemId);

            if (inventoryItem.CurrentStock < returnItem.Quantity)
            {
                throw new InventoryDomainException(
                    $"Insufficient stock to complete return for '{inventoryItem.Name}'. Required: {returnItem.Quantity} {inventoryItem.UnitOfMeasure}, Available: {inventoryItem.CurrentStock} {inventoryItem.UnitOfMeasure}.");
            }

            var itemTxId = Guid.NewGuid();

            // 1. Recalculate WAC and deduct stock in aggregate
            inventoryItem.ReturnToSupplier(returnItem.Quantity, returnItem.UnitCostBdt, itemTxId);
            _itemRepository.Update(inventoryItem);

            // 2. Record StockTransaction audit entry
            var transaction = new StockTransaction(
                id: itemTxId,
                tenantId: purchaseReturn.TenantId,
                farmId: purchaseReturn.FarmId,
                inventoryItemId: returnItem.InventoryItemId,
                transactionType: StockTransactionType.PurchaseReturn,
                quantity: returnItem.Quantity,
                unitCostBdt: returnItem.UnitCostBdt,
                balanceAfter: inventoryItem.CurrentStock,
                transactionDate: purchaseReturn.ReturnDate,
                supplierId: purchaseReturn.SupplierId,
                invoiceNumber: purchaseReturn.CreditNoteNumber ?? purchaseReturn.ReturnNumber,
                reason: $"Purchase Return {purchaseReturn.ReturnNumber} (PO: {po.PoNumber})",
                recordedBy: _currentUserService.UserId?.ToString(),
                referenceId: purchaseReturn.Id);

            await _transactionRepository.AddAsync(transaction, cancellationToken);
        }

        // 3. Mark return completed and raise PurchaseReturnCompletedEvent
        purchaseReturn.Complete();
        var domainEvents = purchaseReturn.DomainEvents.OfType<Farm360.Domain.Inventory.Events.PurchaseReturnCompletedEvent>().ToList();

        await _returnRepository.UpdateAsync(purchaseReturn, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _publisher.Publish(new Farm360.Application.Inventory.EventHandlers.PurchaseReturnCompletedNotification(domainEvent), cancellationToken);
        }
    }
}
