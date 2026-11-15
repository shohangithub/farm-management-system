using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Common;
using Farm360.Domain.Feeding;
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
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IAnimalFeedAllocationRepository _allocationRepository;
    private readonly IFeedAllocationService _allocationService;
    private readonly IInventoryItemRepository _inventoryRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;
    private readonly ILogger<DailyEntryConfirmedEventHandler> _logger;
    private readonly IFarmBusinessRulesProvider? _rules;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;
    private readonly Farm360.Domain.Finance.Interfaces.IAnimalCostLedgerRepository? _ledgerRepository;

    public DailyEntryConfirmedEventHandler(
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalFeedingPlanRepository planRepository,
        IAnimalFeedAllocationRepository allocationRepository,
        IFeedAllocationService allocationService,
        IInventoryItemRepository inventoryRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        IPublisher publisher,
        ILogger<DailyEntryConfirmedEventHandler> logger,
        IFarmBusinessRulesProvider? rules = null,
        Farm360.Application.Inventory.Units.IUnitService? units = null,
        Farm360.Domain.Finance.Interfaces.IAnimalCostLedgerRepository? ledgerRepository = null)
    {
        _ledgerRepository = ledgerRepository;
        _rules = rules;
        _units = units;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _entryRepository = entryRepository;
        _planRepository = planRepository;
        _allocationRepository = allocationRepository;
        _allocationService = allocationService;
        _inventoryRepository = inventoryRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _publisher = publisher;
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

            // A group plan (shed / pen / batch) gives its quantity to every head (or, under the
            // WholeGroup basis, its quantity already is the group's total): stock, cost and the
            // per-animal rows must all read it the same way.
            var plan = await _planRepository.GetByIdAsync(entry.FeedingPlanId, cancellationToken);
            var allocations = (await _allocationRepository.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken)).ToList();
            var isNewAllocation = false;
            if (allocations.Count == 0 && plan != null)
            {
                // Rows normally exist from the moment the entry was generated; build them now if not,
                // so the head count is known before stock is deducted.
                allocations = (await _allocationService.BuildAllocationsAsync(entry, plan, isBackfill: false, cancellationToken)).ToList();
                isNewAllocation = allocations.Count > 0;
            }

            var basis = _rules?.Current.GroupPlanQuantityBasis ?? GroupPlanQuantityBasis.PerHead;
            var heads = FeedEntryHeads.Multiplier(plan, allocations, basis);
            var totalActualKg = notification.ActualKg * heads;

            var transactionId = Guid.NewGuid();
            bool anyDeducted = false;
            decimal totalDeductedCost = 0m;
            decimal stockDeductedCost = 0m;

            // Same calculator the confirm/adjust pre-check uses: % normalised by the recipe's total,
            // kg converted to each item's base unit, priced per kg of feed.
            var calculator = new FeedCostCalculator(_feedIngredientRepository, _inventoryRepository, _units);
            var requirement = await calculator.RequirementAsync(formula, totalActualKg, cancellationToken);

            foreach (var line in requirement.Ingredients)
            {
                if (line.Kg <= 0)
                    continue;

                // An ingredient not tracked in stock (or whose item is gone) is counted at its recipe cost.
                // A stock-tracked one is only counted when it was actually taken out of stock.
                if (line.Item is not { } inventoryItem)
                {
                    totalDeductedCost += line.CostBdt;
                    continue;
                }

                if (!line.IsShort)
                {
                    inventoryItem.DeductStock(line.StockQuantity, transactionId);
                    _inventoryRepository.Update(inventoryItem);

                    var transaction = new Farm360.Domain.Inventory.StockTransaction(
                        id: Guid.NewGuid(),
                        tenantId: notification.TenantId,
                        farmId: notification.FarmId,
                        inventoryItemId: inventoryItem.Id,
                        transactionType: Farm360.Domain.Inventory.Enums.StockTransactionType.PlannedFeedConsumption,
                        quantity: line.StockQuantity,
                        unitCostBdt: inventoryItem.WeightedAverageCostBdt,
                        balanceAfter: inventoryItem.CurrentStock,
                        transactionDate: entry.EntryDate,
                        reason: $"Auto deduction for daily feeding plan entry {entry.Id}",
                        referenceId: entry.Id
                    );

                    await _transactionRepository.AddAsync(transaction, cancellationToken);
                    anyDeducted = true;
                    totalDeductedCost += line.CostBdt;
                    stockDeductedCost += line.CostBdt;
                }
                else if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(
                        "Skipped auto-deduction for ingredient '{ItemName}' on feeding entry {EntryId}: Insufficient stock (Requested: {Requested}, Available: {Available}) or inactive. Its cost is excluded from this entry's total since nothing was actually consumed from inventory.",
                        inventoryItem.Name, entry.Id, line.StockQuantity, inventoryItem.CurrentStock);
                }
            }

            // Unit cost per kg of everything given; SetConsumptionCost multiplies it back by the
            // quantity x heads, so a shortfall ingredient excluded above lowers the total.
            decimal blendedUnitCost = totalActualKg > 0
                ? Math.Round(totalDeductedCost / totalActualKg, 4)
                : await calculator.FormulaCostPerKgAsync(formula, cancellationToken);

            entry.SetConsumptionCost(blendedUnitCost, heads);

            if (anyDeducted)
            {
                entry.SetInventoryTransactionId(transactionId);
            }

            _entryRepository.Update(entry);

            // Per-animal rows carry exactly what was given and what it cost.
            if (allocations.Count > 0)
            {
                var entryCost = entry.TotalCostBdt ?? Math.Round(totalActualKg * blendedUnitCost, 2);

                foreach (var alloc in allocations.Where(a => a.Origin == FeedAllocationOrigin.IndividualPlan))
                {
                    alloc.UpdateConsumption(notification.ActualKg, entryCost, blendedUnitCost);
                }

                // Group shares split the entry's one total via ProportionalSplit so the rows sum to
                // the penny (see FeedAllocationCalculator).
                var groupAllocations = allocations
                    .Where(a => a.Origin != FeedAllocationOrigin.IndividualPlan)
                    .ToList();

                if (groupAllocations.Count > 0)
                {
                    var shareFactors = groupAllocations.Select(a => a.ShareFactor).ToArray();
                    var kgShares = ProportionalSplit.Distribute(totalActualKg, shareFactors, decimals: 4);
                    var costShares = ProportionalSplit.Distribute(entryCost, shareFactors, decimals: 2);

                    for (var i = 0; i < groupAllocations.Count; i++)
                    {
                        groupAllocations[i].UpdateConsumption(kgShares[i], costShares[i], blendedUnitCost);
                    }
                }

                if (isNewAllocation)
                    _allocationRepository.AddRange(allocations);
                else
                    _allocationRepository.UpdateRange(allocations);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Every animal this entry touched gets its ledger feed bucket set (not added to) from
            // its confirmed allocation total, so an adjustment or a repeat can never double count.
            if (_ledgerRepository is not null && allocations.Count > 0)
            {
                await AnimalFeedCostSync.SyncAsync(_allocationRepository, _ledgerRepository, notification.TenantId, notification.FarmId,
                    allocations.Select(a => a.AnimalId), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Stock deduction and cost snapshot (Blended: {BlendedCost} BDT/kg, Total: {TotalCost} BDT) recorded for entry {EntryId}", 
                    blendedUnitCost, entry.TotalCostBdt, entry.Id);
            }

            // Publish DailyFeedingCostCalculatedEvent for automated financial tracking
            if (entry.TotalCostBdt.HasValue && entry.TotalCostBdt.Value > 0)
            {
                await _publisher.Publish(new DailyFeedingCostCalculatedEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    entry.Id,
                    notification.TenantId,
                    notification.FarmId,
                    entry.TotalCostBdt.Value,
                    totalActualKg,
                    entry.EntryDate,
                    plan?.AnimalId,
                    entry.BatchId,
                    entry.ShedId,
                    Math.Min(entry.TotalCostBdt.Value, Math.Round(stockDeductedCost, 2))
                ), cancellationToken);
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
