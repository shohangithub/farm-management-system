using Farm360.Application.Common.Exceptions;
using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.PurchaseReturns;

public sealed record GetPurchaseReturnByIdQuery(Guid Id) : IRequest<PurchaseReturnDto?>;

public sealed class GetPurchaseReturnByIdQueryHandler : IRequestHandler<GetPurchaseReturnByIdQuery, PurchaseReturnDto?>
{
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IInventoryItemRepository _itemRepository;

    public GetPurchaseReturnByIdQueryHandler(
        IPurchaseReturnRepository returnRepository,
        IPurchaseOrderRepository poRepository,
        ISupplierRepository supplierRepository,
        IInventoryItemRepository itemRepository)
    {
        _returnRepository = returnRepository;
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
        _itemRepository = itemRepository;
    }

    public async Task<PurchaseReturnDto?> Handle(GetPurchaseReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var pr = await _returnRepository.GetByIdWithItemsAsync(request.Id, cancellationToken);
        if (pr == null)
            return null;

        var po = await _poRepository.GetByIdAsync(pr.PurchaseOrderId, cancellationToken);
        var supplier = await _supplierRepository.GetByIdAsync(pr.SupplierId, cancellationToken);

        var itemDtos = new List<PurchaseReturnItemDto>();
        foreach (var item in pr.Items)
        {
            var invItem = await _itemRepository.GetByIdAsync(item.InventoryItemId, cancellationToken);
            itemDtos.Add(new PurchaseReturnItemDto(
                item.Id,
                item.PurchaseOrderItemId,
                item.InventoryItemId,
                invItem?.Name,
                invItem?.UnitOfMeasure,
                item.Quantity,
                item.UnitCostBdt,
                item.TotalCostBdt));
        }

        return new PurchaseReturnDto(
            pr.Id,
            pr.FarmId,
            pr.ReturnNumber,
            pr.PurchaseOrderId,
            po?.PoNumber,
            pr.SupplierId,
            supplier?.Name,
            pr.ReturnDate,
            pr.Status,
            pr.Reason,
            pr.Notes,
            pr.CreditNoteNumber,
            pr.TotalAmountBdt,
            itemDtos);
    }
}
