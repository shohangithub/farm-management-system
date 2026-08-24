using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.GetExpiringItems;

public class GetExpiringItemsQueryHandler : IRequestHandler<GetExpiringItemsQuery, List<ExpiringItemDto>>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;

    public GetExpiringItemsQueryHandler(
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository)
    {
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<List<ExpiringItemDto>> Handle(GetExpiringItemsQuery request, CancellationToken cancellationToken)
    {
        var targetDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(request.DaysThreshold));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var transactions = await _transactionRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var items = await _itemRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);

        var itemDict = items.ToDictionary(i => i.Id);

        var expiringTx = transactions
            .Where(t => t.TransactionType == StockTransactionType.StockIn 
                        && t.ExpiryDate != null 
                        && t.ExpiryDate <= targetDate)
            .Where(t => itemDict.TryGetValue(t.InventoryItemId, out var item) && item.IsActive && item.CurrentStock > 0)
            .ToList();

        var result = expiringTx
            .Select(t => {
                var item = itemDict[t.InventoryItemId];
                return new ExpiringItemDto(
                    item.Id,
                    item.Name,
                    item.Category.ToString(),
                    item.UnitOfMeasure,
                    t.BatchNumber,
                    t.ExpiryDate!.Value,
                    t.ExpiryDate.Value.DayNumber - today.DayNumber
                );
            })
            .DistinctBy(x => new { x.ItemId, x.BatchNumber })
            .OrderBy(x => x.DaysUntilExpiry)
            .ToList();

        return result;
    }
}
