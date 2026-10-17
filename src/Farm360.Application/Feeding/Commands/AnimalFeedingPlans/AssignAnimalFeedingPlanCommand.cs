using Farm360.Application.Feeding.Services;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public sealed record AssignAnimalFeedingPlanCommand(
    Guid FarmId,
    IReadOnlyList<Guid> FeedingRuleSetIds,
    FeedingPlanType PlanType,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<Guid>? AnimalIds = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    Guid? PenId = null,
    IReadOnlyList<DirectRation>? DirectRations = null,
    bool ReplaceExistingCharts = false) : IRequest<IReadOnlyList<Guid>>, Farm360.Application.Common.Behaviors.ITransactionalCommand;

public sealed class AssignAnimalFeedingPlanCommandValidator : AbstractValidator<AssignAnimalFeedingPlanCommand>
{
    public AssignAnimalFeedingPlanCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x).Must(x => (x.FeedingRuleSetIds?.Count ?? 0) > 0 || (x.DirectRations?.Count ?? 0) > 0)
            .WithMessage("Choose at least one rule set or add a direct ration.");
        RuleForEach(x => x.DirectRations).ChildRules(r =>
        {
            r.RuleFor(d => d.KgPerDay).GreaterThan(0).WithMessage("Each direct ration needs kg per day.");
            r.RuleFor(d => d).Must(d => d.FormulaId.HasValue || d.InventoryItemId.HasValue)
                .WithMessage("Each direct ration needs a formula or a stock item.");
        });
        RuleFor(x => x.PlanType).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x).Must(x => (x.AnimalIds != null && x.AnimalIds.Count > 0) || x.BatchId.HasValue || x.ShedId.HasValue || x.PenId.HasValue)
            .WithMessage("At least one target (Animal, Batch, Shed, or Pen) must be specified.");
    }
}

public sealed class AssignAnimalFeedingPlanCommandHandler : IRequestHandler<AssignAnimalFeedingPlanCommand, IReadOnlyList<Guid>>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;
    private readonly IDirectFeedService _directFeed;

    public AssignAnimalFeedingPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        IFeedingRuleSetRepository ruleSetRepository,
        IAnimalRepository animalRepository,
        IDailyFeedingEntryRepository entryRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService,
        IDirectFeedService directFeed)
    {
        _directFeed = directFeed;
        _repository = repository;
        _ruleSetRepository = ruleSetRepository;
        _animalRepository = animalRepository;
        _entryRepository = entryRepository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<Guid>> Handle(AssignAnimalFeedingPlanCommand request, CancellationToken cancellationToken)
    {
        var createdPlanIds = new List<Guid>();

        var distinctRuleSetIds = (request.FeedingRuleSetIds ?? []).Distinct().ToList();
        var targetAnimalIds = request.AnimalIds?.Distinct().ToList() ?? new List<Guid>();

        // Validate that all referenced rule sets exist and are currently active
        var ruleSetMap = new Dictionary<Guid, FeedingRuleSet>();
        var ruleSetValidationFailures = new List<ValidationFailure>();

        foreach (var ruleSetId in distinctRuleSetIds)
        {
            var ruleSet = await _ruleSetRepository.GetByIdAsync(ruleSetId, cancellationToken);
            if (ruleSet == null)
            {
                ruleSetValidationFailures.Add(new ValidationFailure("FeedingRuleSetIds", $"Feeding rule set '{ruleSetId}' was not found."));
                continue;
            }

            if (!ruleSet.IsActive)
            {
                ruleSetValidationFailures.Add(new ValidationFailure("FeedingRuleSetIds", $"Feeding rule set '{ruleSet.Name}' is inactive and cannot be assigned to new feeding plans."));
                continue;
            }

            ruleSetMap[ruleSetId] = ruleSet;
        }

        if (ruleSetValidationFailures.Count > 0)
        {
            throw new Farm360.Application.Common.Exceptions.ValidationException(ruleSetValidationFailures);
        }

        // Direct rations ("this formula / stock item, this many kg a day") become one-line
        // fixed-quantity rule sets, reused when the same ration was set up before.
        foreach (var ration in request.DirectRations ?? [])
        {
            var species = ruleSetMap.Values.Select(r => r.Species).FirstOrDefault();
            var direct = await _directFeed.GetOrCreateDirectRuleSetAsync(ration, species, cancellationToken);
            ruleSetMap[direct.Id] = direct;
            if (!distinctRuleSetIds.Contains(direct.Id))
                distinctRuleSetIds.Add(direct.Id);
        }

        // Already on a chosen rule set: an error, unless switching -- then it is simply kept.
        var keep = new HashSet<(Guid AnimalId, Guid RuleSetId)>();
        if (targetAnimalIds.Count > 0 && request.ReplaceExistingCharts)
        {
            var activePlans = await _repository.GetActivePlansByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
            foreach (var plan in activePlans.Where(p => p.AnimalId.HasValue && targetAnimalIds.Contains(p.AnimalId.Value)))
            {
                if (distinctRuleSetIds.Contains(plan.FeedingRuleSetId))
                {
                    keep.Add((plan.AnimalId!.Value, plan.FeedingRuleSetId));
                    continue;
                }

                // "Switch": end the animal's other feed charts. Direct extras (add-ons) stay.
                var other = await _ruleSetRepository.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken);
                var isExtra = other?.Name.StartsWith(DirectFeedService.DirectRuleSetPrefix, StringComparison.Ordinal) ?? false;
                var choosingChart = ruleSetMap.Values.Any(r => !r.Name.StartsWith(DirectFeedService.DirectRuleSetPrefix, StringComparison.Ordinal));
                if (!isExtra && choosingChart)
                {
                    plan.Cancel();
                    _repository.Update(plan);
                }
            }
        }
        else if (targetAnimalIds.Count > 0)
        {
            var activePlans = await _repository.GetActivePlansByFarmAsync(_tenantService.TenantId, request.FarmId, cancellationToken);
            var duplicateConflicts = new List<ValidationFailure>();

            foreach (var ruleSetId in distinctRuleSetIds)
            {
                var ruleSetName = ruleSetMap.TryGetValue(ruleSetId, out var rs) ? rs.Name : "Selected Rule Set";

                foreach (var animalId in targetAnimalIds)
                {
                    if (activePlans.Any(p => p.AnimalId == animalId && p.FeedingRuleSetId == ruleSetId))
                    {
                        var animal = await _animalRepository.GetByIdAsync(animalId, cancellationToken);
                        var tag = animal?.Tag?.TagId ?? animalId.ToString();
                        duplicateConflicts.Add(new ValidationFailure("AnimalIds", $"Animal '{tag}' already has an active feeding plan for '{ruleSetName}'."));
                    }
                }
            }

            if (duplicateConflicts.Count > 0)
            {
                throw new Farm360.Application.Common.Exceptions.ValidationException(duplicateConflicts);
            }
        }

        var targets = new List<Guid?>();
        if (targetAnimalIds.Count > 0)
        {
            targets.AddRange(targetAnimalIds.Cast<Guid?>());
        }
        else
        {
            targets.Add(null); // to run at least once for Batch/Shed/Pen
        }

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

        foreach (var ruleSetId in distinctRuleSetIds)
        {
            if (!ruleSetMap.TryGetValue(ruleSetId, out var ruleSet) || !ruleSet.IsActive)
                continue;

            foreach (var animalId in targets)
            {
                if (animalId.HasValue && keep.Contains((animalId.Value, ruleSetId)))
                    continue;

                var plan = new AnimalFeedingPlan(
                    Guid.NewGuid(),
                    _tenantService.TenantId,
                    request.FarmId,
                    ruleSetId,
                    request.PlanType,
                    request.StartDate,
                    request.EndDate,
                    animalId,
                    request.BatchId,
                    request.ShedId,
                    request.PenId);

                decimal currentWeight = 0;
                int? ageDays = null;
                var today = DateOnly.FromDateTime(DateTime.UtcNow);

                if (animalId.HasValue)
                {
                    var animal = await _animalRepository.GetByIdAsync(animalId.Value, cancellationToken);
                    currentWeight = animal?.LatestWeightKg ?? 0;
                    if (animal is not null)
                        ageDays = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, today);
                }

                var matchingRules = FeedingRuleMatcher.Match(ruleSet, currentWeight, ageDays).Lines;
                var matchingRule = matchingRules.Count > 0 ? matchingRules[0] : null;

                if (matchingRule != null)
                {
                    var expectedKg = FeedingRuleMatcher.ExpectedKg(ruleSet, matchingRule, currentWeight);

                    plan.UpdateCurrentRule(
                        matchingRule.Id,
                        currentWeight,
                        expectedKg,
                        matchingRule.RoughageKgPerDay
                    );
                }

                await _repository.AddAsync(plan, cancellationToken);
                createdPlanIds.Add(plan.Id);

                // If plan is active today, immediately generate daily feeding entries for today
                if (request.StartDate <= today && (!request.EndDate.HasValue || request.EndDate.Value >= today))
                {
                    foreach (var ruleLine in matchingRules)
                    {
                        var expKg = FeedingRuleMatcher.ExpectedKg(ruleSet, ruleLine, currentWeight);

                        var entry = new DailyFeedingEntry(
                            id: Guid.NewGuid(),
                            tenantId: _tenantService.TenantId,
                            feedingPlanId: plan.Id,
                            farmId: request.FarmId,
                            entryDate: today,
                            formulaId: ruleLine.FormulaId,
                            expectedKg: expKg,
                            ruleLineId: ruleLine.Id,
                            shedId: request.ShedId,
                            penId: request.PenId,
                            batchId: request.BatchId
                        );

                        decimal unitCost = await ResolveUnitCostAsync(ruleLine.FormulaId);
                        entry.SetConsumptionCost(unitCost);

                        await _entryRepository.AddAsync(entry, cancellationToken);
                    }
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return createdPlanIds;
    }
}

