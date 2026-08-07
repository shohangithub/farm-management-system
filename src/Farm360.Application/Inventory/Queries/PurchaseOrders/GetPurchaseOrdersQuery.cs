using Farm360.Application.Common.Models;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.PurchaseOrders;

public record GetPurchaseOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? SupplierId = null,
    PurchaseOrderStatus? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<PurchaseOrderDto>>;

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, PagedResult<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _repository;

    public GetPurchaseOrdersQueryHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.SupplierId,
            request.Status,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(po => new PurchaseOrderDto(
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
                i.TotalCostBdt)).ToList())).ToList();

        return new PagedResult<PurchaseOrderDto>(dtos, count, pageNumber, pageSize);
    }
}
