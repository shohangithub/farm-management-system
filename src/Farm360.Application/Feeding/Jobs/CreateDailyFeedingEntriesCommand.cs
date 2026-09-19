using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Application.Feeding.Services;
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
    private readonly IFeedAllocationService _allocationService;
    private readonly Farm360.Domain.Feeding.Interfaces.Repositories.IAnimalFeedAllocationRepository _allocationRepository;
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
        IFeedAllocationService allocationService,
        Farm360.Domain.Feeding.Interfaces.Repositories.IAnimalFeedAllocationRepository allocationRepository,
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
        _allocationService = allocationService;
        _allocationRepository = allocationRepository;
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
        var ruleSets = new Dictionary<Guid, FeedingRuleSet?>();
        var allocatedAnimals = 0;
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
            // Skip plans where today is outside the plan's validity window
            if (today < plan.StartDate || (plan.EndDate.HasValue && today > plan.EndDate.Value))
                continue;

            // Skip plans that have an exclusion for today
            if (plan.Exclusions.Any(e => e.ExclusionDate == today && (!e.ResumesOn.HasValue || today < e.ResumesOn.Value)))
                continue;

            if (!ruleSets.TryGetValue(plan.FeedingRuleSetId, out var ruleSet))
            {
                ruleSet = await _ruleSetRepository.GetByIdAcrossTenantsAsync(plan.FeedingRuleSetId, cancellationToken);
                ruleSets[plan.FeedingRuleSetId] = ruleSet;
            }

            // Skip plan if the feeding rule set does not exist or is inactive
            if (ruleSet == null || !ruleSet.IsActive)
                continue;

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

                // Fan the plan-level entry out to the animals it actually feeds (docs/32 GAP-1).
                // Written here, at the moment feed is recorded, because herd composition and
                // weights move: deriving the split later would silently change past reports.
                var allocations = await _allocationService.BuildAllocationsAsync(entry, plan, isBackfill: false, cancellationToken);
                if (allocations.Count > 0)
                {
                    _allocationRepository.AddRange(allocations);
                    allocatedAnimals += allocations.Count;
                }
                else if (_logger.IsEnabled(LogLevel.Warning))
                {
                    // Feed was booked against a scope that holds no active animals. Worth saying
                    // out loud: the cost exists but no animal will ever carry it.
                    _logger.LogWarning(
                        "Feeding entry {EntryId} on plan {PlanId} allocated to no animals (batch {BatchId}, shed {ShedId}, pen {PenId}).",
                        entry.Id, plan.Id, plan.BatchId, plan.ShedId, plan.PenId);
                }
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Daily feeding entries created; {AllocationCount} per-animal feed allocations written.", allocatedAnimals);
    }
}
