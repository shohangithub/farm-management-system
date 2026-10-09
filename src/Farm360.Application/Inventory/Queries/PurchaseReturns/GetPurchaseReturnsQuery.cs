using Farm360.Application.Common.Models;
using Farm360.Application.Inventory.DTOs;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.PurchaseReturns;

public sealed record GetPurchaseReturnsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    Guid? PurchaseOrderId = null,
    Guid? SupplierId = null,
    PurchaseReturnStatus? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = true) : IRequest<PagedResult<PurchaseReturnDto>>;

public sealed class GetPurchaseReturnsQueryHandler : IRequestHandler<GetPurchaseReturnsQuery, PagedResult<PurchaseReturnDto>>
{
    private readonly IPurchaseReturnRepository _returnRepository;
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly ISupplierRepository _supplierRepository;

    public GetPurchaseReturnsQueryHandler(
        IPurchaseReturnRepository returnRepository,
        IPurchaseOrderRepository poRepository,
        ISupplierRepository supplierRepository)
    {
        _returnRepository = returnRepository;
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
    }

    public async Task<PagedResult<PurchaseReturnDto>> Handle(GetPurchaseReturnsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _returnRepository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.PurchaseOrderId,
            request.SupplierId,
            request.Status,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var poIds = items.Select(x => x.PurchaseOrderId).Distinct().ToList();
        var supplierIds = items.Select(x => x.SupplierId).Distinct().ToList();

        var poMap = new Dictionary<Guid, string>();
        foreach (var poId in poIds)
        {
            var po = await _poRepository.GetByIdAsync(poId, cancellationToken);
            if (po != null)
                poMap[poId] = po.PoNumber;
        }

        var supplierMap = new Dictionary<Guid, string>();
        foreach (var supplierId in supplierIds)
        {
            var supplier = await _supplierRepository.GetByIdAsync(supplierId, cancellationToken);
            if (supplier != null)
                supplierMap[supplierId] = supplier.Name;
        }

        var dtos = items.Select(pr => new PurchaseReturnDto(
            pr.Id,
            pr.FarmId,
            pr.ReturnNumber,
            pr.PurchaseOrderId,
            poMap.GetValueOrDefault(pr.PurchaseOrderId),
            pr.SupplierId,
            supplierMap.GetValueOrDefault(pr.SupplierId),
            pr.ReturnDate,
            pr.Status,
            pr.Reason,
            pr.Notes,
            pr.CreditNoteNumber,
            pr.TotalAmountBdt,
            pr.Items.Select(i => new PurchaseReturnItemDto(
                i.Id,
                i.PurchaseOrderItemId,
                i.InventoryItemId,
                null,
                null,
                i.Quantity,
                i.UnitCostBdt,
                i.TotalCostBdt)).ToList()
        )).ToList();

        return new PagedResult<PurchaseReturnDto>(dtos, count, pageNumber, pageSize);
    }
}
