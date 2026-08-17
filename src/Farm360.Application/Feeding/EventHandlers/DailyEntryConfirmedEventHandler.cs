using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class DailyEntryConfirmedEventHandler : INotificationHandler<DailyEntryConfirmedEvent>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyEntryConfirmedEventHandler> _logger;

    public DailyEntryConfirmedEventHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IDailyFeedingEntryRepository entryRepository,
        IInventoryItemRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyEntryConfirmedEventHandler> logger)
    {
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _entryRepository = entryRepository;
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyEntryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing DailyEntryConfirmedEvent for EntryId: {EntryId}", notification.EntryId);

        var entry = await _entryRepository.GetByIdAsync(notification.EntryId, cancellationToken);
            
        if (entry == null) return;

        var formula = await _formulaRepository.GetByIdAsync(entry.FormulaId, cancellationToken);

        if (formula == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Formula {FormulaId} not found for feeding entry {EntryId}. Cannot deduct stock.", entry.FormulaId, entry.Id);
            return;
        }

        var transactionId = Guid.NewGuid();
        bool anyDeducted = false;

        foreach (var formulaIngredient in formula.Ingredients)
        {
            var feedIngredient = await _feedIngredientRepository.GetByIdAsync(formulaIngredient.IngredientId, cancellationToken);
            if (feedIngredient?.InventoryItemId != null)
            {
                var inventoryItem = await _inventoryRepository.GetByIdAsync(feedIngredient.InventoryItemId.Value, cancellationToken);
                
                if (inventoryItem != null)
                {
                    // Calculate expected quantity based on percentage
                    var deductionQty = notification.ActualKg * (formulaIngredient.Percentage / 100m);
                    
                    if (deductionQty > 0)
                    {
                        var cost = inventoryItem.WeightedAverageCostBdt;
                        inventoryItem.DeductStock(deductionQty, transactionId);
                        _inventoryRepository.Update(inventoryItem);

                        var transaction = new Farm360.Domain.Inventory.StockTransaction(
                            id: Guid.NewGuid(),
                            tenantId: notification.TenantId,
                            farmId: notification.FarmId,
                            inventoryItemId: inventoryItem.Id,
                            transactionType: Farm360.Domain.Inventory.Enums.StockTransactionType.PlannedFeedConsumption,
                            quantity: deductionQty,
                            unitCostBdt: cost,
                            balanceAfter: inventoryItem.CurrentStock,
                            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
                            reason: $"Auto deduction for daily feeding plan entry {entry.Id}",
                            referenceId: Guid.TryParse(entry.Id.ToString(), out var refId) ? refId : null
                        );
                        
                        await _transactionRepository.AddAsync(transaction, cancellationToken);
                        anyDeducted = true;
                    }
                }
            }
        }

        if (anyDeducted)
        {
            entry.SetInventoryTransactionId(transactionId);
            _entryRepository.Update(entry);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Stock deducted successfully for feeding entry {EntryId}", entry.Id);
        }
    }
}
