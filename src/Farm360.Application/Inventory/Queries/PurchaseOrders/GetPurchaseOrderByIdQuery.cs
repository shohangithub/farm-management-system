using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.PurchaseOrders;

public record GetPurchaseOrderByIdQuery(Guid Id) : IRequest<PurchaseOrderDto>;

public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, PurchaseOrderDto>
{
    private readonly IPurchaseOrderRepository _repository;

    public GetPurchaseOrderByIdQueryHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PurchaseOrderDto> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var po = await _repository.GetByIdWithItemsAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.Id);

        return new PurchaseOrderDto(
            po.Id,
            po.FarmId,
            po.PoNumber,
            po.SupplierId,
            po.Status,
            po.OrderDate,
            po.ExpectedDeliveryDate,
            po.Notes,
            po.TotalAmountBdt,
            po.Items.Select(i => new PurchaseOrderItemDto(
                i.Id,
                i.InventoryItemId,
                i.Quantity,
                i.UnitCostBdt,
                i.TotalCostBdt)).ToList());
    }
}
