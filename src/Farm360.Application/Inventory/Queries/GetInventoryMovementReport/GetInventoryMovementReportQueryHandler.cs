using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Models;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Inventory.Queries.GetInventoryMovementReport;

public class GetInventoryMovementReportQueryHandler : IRequestHandler<GetInventoryMovementReportQuery, InventoryMovementReportDto>
{
    private readonly IInventoryItemRepository _itemRepository;
    private readonly IStockTransactionRepository _transactionRepository;

    public GetInventoryMovementReportQueryHandler(
        IInventoryItemRepository itemRepository,
        IStockTransactionRepository transactionRepository)
    {
        _itemRepository = itemRepository;
        _transactionRepository = transactionRepository;
    }

    public async Task<InventoryMovementReportDto> Handle(GetInventoryMovementReportQuery request, CancellationToken cancellationToken)
    {
        var items = await _itemRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var transactions = await _transactionRepository.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);

        var activeItems = items.Where(x => x.IsActive).ToList();
        var reportItems = new List<InventoryMovementItemDto>();

        foreach (var item in activeItems)
        {
            var itemTx = transactions.Where(t => t.InventoryItemId == item.Id).ToList();

            // Opening stock = sum of received - sum of consumed/writeoff BEFORE start date
            var txBeforeStart = itemTx.Where(t => t.TransactionDate < request.StartDate).ToList();
            var openingReceived = txBeforeStart.Where(t => t.TransactionType == StockTransactionType.StockIn).Sum(t => t.Quantity);
            var openingDeducted = txBeforeStart.Where(t => 
                t.TransactionType == StockTransactionType.ManualStockOut || 
                t.TransactionType == StockTransactionType.AutoFeedConsumption || 
                t.TransactionType == StockTransactionType.AutoMedicineConsumption ||
                t.TransactionType == StockTransactionType.PlannedFeedConsumption ||
                t.TransactionType == StockTransactionType.WriteOff || 
                t.TransactionType == StockTransactionType.Adjustment ||
                t.TransactionType == StockTransactionType.ReconciliationAdjustment).Sum(t => t.Quantity);
            var openingStock = openingReceived - openingDeducted;
            
            var txInRange = itemTx.Where(t => t.TransactionDate >= request.StartDate && t.TransactionDate <= request.EndDate).ToList();
            
            var received = txInRange.Where(t => t.TransactionType == StockTransactionType.StockIn).Sum(t => t.Quantity);
            var consumed = txInRange.Where(t => 
                t.TransactionType == StockTransactionType.ManualStockOut ||
                t.TransactionType == StockTransactionType.AutoFeedConsumption ||
                t.TransactionType == StockTransactionType.AutoMedicineConsumption ||
                t.TransactionType == StockTransactionType.PlannedFeedConsumption).Sum(t => t.Quantity);
            var writtenOff = txInRange.Where(t => t.TransactionType == StockTransactionType.WriteOff).Sum(t => t.Quantity);
            
            var closingStock = openingStock + received - consumed - writtenOff;

            reportItems.Add(new InventoryMovementItemDto(
                item.Id,
                item.Name,
                item.Category.ToString(),
                item.UnitOfMeasure,
                openingStock,
                received,
                consumed,
                writtenOff,
                closingStock,
                closingStock * item.WeightedAverageCostBdt
            ));
        }

        return new InventoryMovementReportDto(
            request.StartDate,
            request.EndDate,
            reportItems.OrderBy(x => x.Category).ThenBy(x => x.ItemName).ToList()
        );
    }
}
