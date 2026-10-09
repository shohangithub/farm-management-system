using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Commands.PurchaseReturns;

public sealed record CreatePurchaseReturnCommand(
    Guid FarmId,
    Guid PurchaseOrderId,
    DateOnly ReturnDate,
    PurchaseReturnReason Reason,
    string? CreditNoteNumber,
    string? Notes,
    IReadOnlyList<CreatePurchaseReturnItemRequest> Items) : IRequest<Guid>, ITransactionalCommand;

public sealed class CreatePurchaseReturnCommandHandler : IRequestHandler<CreatePurchaseReturnCommand, Guid>
{
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IInventoryItemRepository _itemRepository;
    private readonly ITenantService _tenantService;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePurchaseReturnCommandHandler(
        IPurchaseReturnRepository returnRepository,
        IPurchaseOrderRepository poRepository,
        IInventoryItemRepository itemRepository,
        ITenantService tenantService,
        IUnitOfWork unitOfWork)
    {
        _returnRepository = returnRepository;
        _poRepository = poRepository;
        _itemRepository = itemRepository;
        _tenantService = tenantService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(CreatePurchaseReturnCommand request, CancellationToken cancellationToken)
    {
        var po = await _poRepository.GetByIdWithItemsAsync(request.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("PurchaseOrder", request.PurchaseOrderId);

        if (po.FarmId != request.FarmId)
            throw new InventoryDomainException("Purchase order does not belong to the specified farm.");

        if (po.Status != PurchaseOrderStatus.Fulfilled)
            throw new InventoryDomainException($"Cannot create a return for a purchase order in '{po.Status}' status. Only fulfilled orders can be returned.");

        if (request.Items.Count == 0)
            throw new InventoryDomainException("At least one item must be returned.");

        var alreadyReturnedMap = await _returnRepository.GetReturnedQuantitiesByPoAsync(po.Id, cancellationToken);

        var purchaseReturn = new PurchaseReturn(
            id: Guid.NewGuid(),
            tenantId: _tenantService.TenantId,
            farmId: request.FarmId,
            purchaseOrderId: po.Id,
            supplierId: po.SupplierId,
            returnDate: request.ReturnDate,
            reason: request.Reason,
            creditNoteNumber: request.CreditNoteNumber,
            notes: request.Notes);

        foreach (var reqItem in request.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == reqItem.PurchaseOrderItemId)
                ?? throw new InventoryDomainException($"Purchase order item '{reqItem.PurchaseOrderItemId}' not found in PO '{po.PoNumber}'.");

            alreadyReturnedMap.TryGetValue(poItem.Id, out var alreadyReturned);
            var maxReturnable = poItem.Quantity - alreadyReturned;

            if (reqItem.Quantity > maxReturnable)
                throw new InventoryDomainException($"Requested return quantity ({reqItem.Quantity}) exceeds maximum returnable quantity ({maxReturnable}) for item.");

            var inventoryItem = await _itemRepository.GetByIdAsync(poItem.InventoryItemId, cancellationToken)
                ?? throw new NotFoundException("InventoryItem", poItem.InventoryItemId);

            if (reqItem.Quantity > inventoryItem.CurrentStock)
                throw new InventoryDomainException($"Requested return quantity ({reqItem.Quantity} {inventoryItem.UnitOfMeasure}) exceeds current available stock ({inventoryItem.CurrentStock} {inventoryItem.UnitOfMeasure}) for '{inventoryItem.Name}'.");

            purchaseReturn.AddItem(poItem.Id, poItem.InventoryItemId, reqItem.Quantity, poItem.UnitCostBdt);
        }

        await _returnRepository.AddAsync(purchaseReturn, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return purchaseReturn.Id;
    }
}
