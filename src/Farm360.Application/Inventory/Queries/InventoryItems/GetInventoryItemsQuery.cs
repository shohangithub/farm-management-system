using Farm360.Application.Common.Models;
using Farm360.Application.Inventory.DTOs;
using Farm360.Application.Inventory.Mappings;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.InventoryItems;

public sealed record GetInventoryItemsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FarmId = null,
    InventoryCategory? Category = null,
    InventoryStatus? Status = null,
    string? Search = null,
    string? SortBy = null,
    bool SortDesc = false) : IRequest<PagedResult<InventoryItemDto>>;

public sealed class GetInventoryItemsQueryHandler : IRequestHandler<GetInventoryItemsQuery, PagedResult<InventoryItemDto>>
{
    private readonly IInventoryItemRepository _repository;

    public GetInventoryItemsQueryHandler(IInventoryItemRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResult<InventoryItemDto>> Handle(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, count) = await _repository.GetPagedAsync(
            pageNumber,
            pageSize,
            request.FarmId,
            request.Category,
            request.Status,
            request.Search,
            request.SortBy,
            request.SortDesc,
            cancellationToken);

        var dtos = items.Select(x => x.ToDto()).ToList();
        return new PagedResult<InventoryItemDto>(dtos, count, pageNumber, pageSize);
    }
}
