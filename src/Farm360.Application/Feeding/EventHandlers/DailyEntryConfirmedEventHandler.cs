using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class DailyEntryConfirmedEventHandler : INotificationHandler<DailyEntryConfirmedEvent>
{
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyEntryConfirmedEventHandler> _logger;

    public DailyEntryConfirmedEventHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalFeedingPlanRepository planRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IInventoryItemRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyEntryConfirmedEventHandler> logger)
    {
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _entryRepository = entryRepository;
        _planRepository = planRepository;
        _ledgerRepository = ledgerRepository;
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyEntryConfirmedEvent notification, CancellationToken cancellationToken)
    {
        try
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
            decimal totalDeductedCost = 0m;
            decimal totalDeductedQty = 0m;

            foreach (var formulaIngredient in formula.Ingredients)
            {
                var feedIngredient = await _feedIngredientRepository.GetByIdAsync(formulaIngredient.IngredientId, cancellationToken);
                decimal cost = formulaIngredient.IngredientCostPerKg;
                var deductionQty = notification.ActualKg * (formulaIngredient.Percentage / 100m);

                if (feedIngredient?.InventoryItemId != null)
                {
                    var inventoryItem = await _inventoryRepository.GetByIdAsync(feedIngredient.InventoryItemId.Value, cancellationToken);
                    
                    if (inventoryItem != null)
                    {
                        if (inventoryItem.WeightedAverageCostBdt > 0)
                        {
                            cost = inventoryItem.WeightedAverageCostBdt;
                        }
                        
                        if (deductionQty > 0)
                        {
                            if (inventoryItem.IsActive && inventoryItem.CurrentStock >= deductionQty)
                            {
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
                            else
                            {
                                if (_logger.IsEnabled(LogLevel.Warning))
                                {
                                    _logger.LogWarning(
                                        "Skipped auto-deduction for ingredient '{ItemName}' on feeding entry {EntryId}: Insufficient stock (Requested: {Requested} kg, Available: {Available} kg) or inactive.",
                                        inventoryItem.Name, entry.Id, deductionQty, inventoryItem.CurrentStock);
                                }
                            }
                        }
                    }
                }

                if (deductionQty > 0)
                {
                    totalDeductedCost += deductionQty * cost;
                    totalDeductedQty += deductionQty;
                }
            }

            decimal blendedUnitCost = totalDeductedQty > 0 
                ? Math.Round(totalDeductedCost / totalDeductedQty, 2) 
                : formula.TotalCostPerKgBdt;

            entry.SetConsumptionCost(blendedUnitCost);

            if (anyDeducted)
            {
                entry.SetInventoryTransactionId(transactionId);
            }

            _entryRepository.Update(entry);

            // Synchronize AnimalCostLedger if plan belongs to an individual animal
            try
            {
                var plan = await _planRepository.GetByIdAsync(entry.FeedingPlanId, cancellationToken);
                if (plan?.AnimalId != null)
                {
                    var animalId = plan.AnimalId.Value;
                    var animalEntries = await _entryRepository.GetEntriesByAnimalIdAsync(notification.TenantId, animalId, cancellationToken);
                    
                    decimal trueTotalFeedCost = animalEntries
                        .Where(e => (e.Status == Farm360.Domain.Feeding.Enums.DailyFeedingEntryStatus.Confirmed || 
                                     e.Status == Farm360.Domain.Feeding.Enums.DailyFeedingEntryStatus.Adjusted) && 
                                    e.TotalCostBdt.HasValue)
                        .Sum(e => e.TotalCostBdt!.Value);

                    // Also include current entry if not yet saved/retrieved in animalEntries
                    if (!animalEntries.Any(e => e.Id == entry.Id) && entry.TotalCostBdt.HasValue)
                    {
                        trueTotalFeedCost += entry.TotalCostBdt.Value;
                    }

                    var ledger = await _ledgerRepository.GetByAnimalIdAsync(animalId, cancellationToken);
                    if (ledger == null)
                    {
                        ledger = Farm360.Domain.Finance.AnimalCostLedger.Create(notification.TenantId, animalId, notification.FarmId);
                        ledger.UpdateFeedCost(trueTotalFeedCost);
                        _ledgerRepository.Add(ledger);
                    }
                    else
                    {
                        ledger.UpdateFeedCost(trueTotalFeedCost);
                        _ledgerRepository.Update(ledger);
                    }
                }
            }
#pragma warning disable CA1031
            catch (Exception ledgerEx)
            {
                _logger.LogWarning(ledgerEx, "Failed to synchronize AnimalCostLedger for entry {EntryId}", entry.Id);
            }
#pragma warning restore CA1031

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Stock deduction and cost snapshot (Blended: {BlendedCost} BDT/kg, Total: {TotalCost} BDT) recorded for entry {EntryId}", 
                    blendedUnitCost, entry.TotalCostBdt, entry.Id);
            }
        }
#pragma warning disable CA1031 // Prevent background event handler from crashing the pipeline
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process stock deduction or cost snapshot for feeding entry {EntryId}", notification.EntryId);
        }
#pragma warning restore CA1031
    }
}
