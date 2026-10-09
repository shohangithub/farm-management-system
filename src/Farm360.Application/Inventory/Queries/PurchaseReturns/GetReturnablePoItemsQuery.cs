using Farm360.Application.Common.Exceptions;
using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Exceptions;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.PurchaseReturns;

public sealed record GetReturnablePoItemsQuery(Guid PurchaseOrderId) : IRequest<IReadOnlyList<ReturnablePoItemDto>>;

public sealed class GetReturnablePoItemsQueryHandler : IRequestHandler<GetReturnablePoItemsQuery, IReadOnlyList<ReturnablePoItemDto>>
{
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IInventoryItemRepository _itemRepository;

    public GetReturnablePoItemsQueryHandler(
        IPurchaseOrderRepository poRepository,
        IPurchaseReturnRepository returnRepository,
        IInventoryItemRepository itemRepository)
    {
        _poRepository = poRepository;
        _returnRepository = returnRepository;
        _itemRepository = itemRepository;
    }

    public async Task<IReadOnlyList<ReturnablePoItemDto>> Handle(GetReturnablePoItemsQuery request, CancellationToken cancellationToken)
    {
        var po = await _poRepository.GetByIdWithItemsAsync(request.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException("PurchaseOrder", request.PurchaseOrderId);

        if (po.Status != PurchaseOrderStatus.Fulfilled)
            throw new InventoryDomainException($"Cannot retrieve returnable items for purchase order in '{po.Status}' status. Only fulfilled orders can be returned.");

        var returnedQuantitiesMap = await _returnRepository.GetReturnedQuantitiesByPoAsync(po.Id, cancellationToken);

        var result = new List<ReturnablePoItemDto>();

        foreach (var poItem in po.Items)
        {
            var invItem = await _itemRepository.GetByIdAsync(poItem.InventoryItemId, cancellationToken);
            var itemName = invItem?.Name ?? "Unknown Item";
            var uom = invItem?.UnitOfMeasure ?? "unit";
            var currentStock = invItem?.CurrentStock ?? 0m;

            returnedQuantitiesMap.TryGetValue(poItem.Id, out var alreadyReturned);
            var unreturnedQty = Math.Max(0, poItem.Quantity - alreadyReturned);
            var maxReturnable = Math.Min(unreturnedQty, currentStock);

            result.Add(new ReturnablePoItemDto(
                poItem.Id,
                poItem.InventoryItemId,
                itemName,
                uom,
                poItem.Quantity,
                alreadyReturned,
                currentStock,
                maxReturnable,
                poItem.UnitCostBdt));
        }

        return result;
    }
}
