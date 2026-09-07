using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Jobs;

public sealed record CreateDailyFeedingEntriesCommand() : IRequest;

public sealed class CreateDailyFeedingEntriesCommandHandler : IRequestHandler<CreateDailyFeedingEntriesCommand>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDailyFeedingEntriesCommandHandler> _logger;

    public CreateDailyFeedingEntriesCommandHandler(
        IAnimalFeedingPlanRepository planRepository,
        IFeedingRuleSetRepository ruleSetRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalRepository animalRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateDailyFeedingEntriesCommandHandler> logger)
    {
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _entryRepository = entryRepository;
        _animalRepository = animalRepository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CreateDailyFeedingEntriesCommand request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Creating daily feeding entries across all tenants");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var activePlans = await _planRepository.GetAllActivePlansAcrossTenantsAsync(cancellationToken);
        var existingPlanIds = await _entryRepository.GetEntryPlanIdsAcrossTenantsByDateAsync(today, cancellationToken);
        var ruleSets = new Dictionary<Guid, FeedingRuleSet>();
        var formulaCostCache = new Dictionary<Guid, decimal>();

        async Task<decimal> ResolveUnitCostAsync(Guid formulaId)
        {
            if (formulaCostCache.TryGetValue(formulaId, out var cached))
                return cached;

            var formula = await _formulaRepository.GetByIdAsync(formulaId, cancellationToken);
            if (formula == null)
            {
                formulaCostCache[formulaId] = 0m;
                return 0m;
            }

            decimal cost = formula.TotalCostPerKgBdt;

            if (formula.Ingredients.Count > 0)
            {
                decimal totalWeightedCost = 0m;
                decimal totalPercentage = formula.Ingredients.Sum(i => i.Percentage);
                if (totalPercentage <= 0) totalPercentage = 100m;

                bool hasInventoryCost = false;
                foreach (var fi in formula.Ingredients)
                {
                    decimal ingCost = fi.IngredientCostPerKg;
                    var feedIng = await _feedIngredientRepository.GetByIdAsync(fi.IngredientId, cancellationToken);
                    if (feedIng?.InventoryItemId != null)
                    {
                        var invItem = await _inventoryRepository.GetByIdAsync(feedIng.InventoryItemId.Value, cancellationToken);
                        if (invItem != null && invItem.WeightedAverageCostBdt > 0)
                        {
                            ingCost = invItem.WeightedAverageCostBdt;
                            hasInventoryCost = true;
                        }
                    }
                    totalWeightedCost += ingCost * (fi.Percentage / totalPercentage);
                }

                if (hasInventoryCost && totalWeightedCost > 0)
                {
                    cost = Math.Round(totalWeightedCost, 2);
                }
            }

            formulaCostCache[formulaId] = cost;
            return cost;
        }

        foreach (var plan in activePlans)
        {
            if (!ruleSets.TryGetValue(plan.FeedingRuleSetId, out var ruleSet))
            {
                var fetchedRuleSet = await _ruleSetRepository.GetByIdAcrossTenantsAsync(plan.FeedingRuleSetId, cancellationToken);
                if (fetchedRuleSet != null)
                {
                    ruleSets[plan.FeedingRuleSetId] = fetchedRuleSet;
                    ruleSet = fetchedRuleSet;
                }
            }

            if (ruleSet == null) continue;

            decimal currentWeight = plan.TriggeredByWeightKg ?? 0;

            var matchingRules = ruleSet.Lines
                .Where(l => currentWeight >= l.WeightFromKg && currentWeight < l.WeightToKg)
                .ToList();

            if (matchingRules.Count == 0 && ruleSet.Lines.Count > 0)
            {
                var minWeight = ruleSet.Lines.Min(l => l.WeightFromKg);
                matchingRules = ruleSet.Lines.Where(l => l.WeightFromKg == minWeight).ToList();
            }

            foreach (var ruleLine in matchingRules)
            {
                if (existingPlanIds.Contains((plan.Id, ruleLine.Id)))
                    continue;

                decimal expectedKg = ruleLine.ConcentrateKgPerDay;

                if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
                {
                    expectedKg = (currentWeight * ruleLine.ConcentrateKgPerDay) / 100m;
                }

                var entry = new DailyFeedingEntry(
                    id: Guid.NewGuid(),
                    tenantId: plan.TenantId,
                    feedingPlanId: plan.Id,
                    farmId: plan.FarmId,
                    entryDate: today,
                    formulaId: ruleLine.FormulaId,
                    expectedKg: expectedKg,
                    ruleLineId: ruleLine.Id,
                    shedId: plan.ShedId,
                    penId: plan.PenId,
                    batchId: plan.BatchId
                );

                decimal unitCost = await ResolveUnitCostAsync(ruleLine.FormulaId);
                entry.SetConsumptionCost(unitCost);

                await _entryRepository.AddAsync(entry, cancellationToken);
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
