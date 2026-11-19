using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.AnimalFeedingPlans;

public sealed record GetFeedingPlansQuery(Guid FarmId, string? Status) : IRequest<IReadOnlyList<AnimalFeedingPlanDto>>;

public sealed record AnimalFeedingPlanDto(
    Guid Id,
    Guid AnimalId,
    string AnimalTag,
    Guid RuleSetId,
    string RuleSetName,
    DateOnly AssignedOn,
    DateOnly? CanceledOn,
    bool IsActive,
    decimal ExpectedDailyFeedKg,
    decimal ConcentrateKgPerDay,
    decimal RoughageKgPerDay,
    decimal? AnimalWeightKg,
    string? AnimalSpecies,
    string? FormulaName,
    decimal EstimatedCostPerKgBdt,
    decimal EstimatedDailyCostBdt,
    IReadOnlyList<object> Exclusions,
    int HeadCount = 1,
    string CostBasis = "Concentrate ration only");

internal sealed class GetFeedingPlansQueryHandler : IRequestHandler<GetFeedingPlansQuery, IReadOnlyList<AnimalFeedingPlanDto>>
{
    private readonly IFarmClock _clock;

    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository _animalRepository;
    private readonly ITenantService _tenantService;
    private readonly Farm360.Application.Feeding.Services.FeedCostCalculator? _calculator;
    private readonly IFarmBusinessRulesProvider? _rules;

    public GetFeedingPlansQueryHandler(
        IAnimalFeedingPlanRepository repository, 
        IFeedingRuleSetRepository ruleSetRepository, 
        IFeedFormulaRepository formulaRepository,
        Farm360.Domain.Livestock.Repositories.IAnimalRepository animalRepository,
        ITenantService tenantService,
        IFeedIngredientRepository? ingredientRepository = null,
        Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository? inventoryRepository = null,
        IFarmBusinessRulesProvider? rules = null,
        Farm360.Application.Inventory.Units.IUnitService? units = null,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _calculator = ingredientRepository is not null && inventoryRepository is not null
            ? new Farm360.Application.Feeding.Services.FeedCostCalculator(ingredientRepository, inventoryRepository, units)
            : null;
        _rules = rules;
        _repository = repository;
        _ruleSetRepository = ruleSetRepository;
        _formulaRepository = formulaRepository;
        _animalRepository = animalRepository;
        _tenantService = tenantService;
    }
    
    public async Task<IReadOnlyList<AnimalFeedingPlanDto>> Handle(GetFeedingPlansQuery request, CancellationToken cancellationToken)
    {
        FeedingPlanStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<FeedingPlanStatus>(request.Status, true, out var parsed))
            {
                statusFilter = parsed;
            }
        }

        var plans = await _repository.GetPlansByFarmAsync(_tenantService.TenantId, request.FarmId, statusFilter, cancellationToken);
        var ruleSets = await _ruleSetRepository.GetAllAsync(cancellationToken);
        var formulas = await _formulaRepository.GetListAsync(_tenantService.TenantId, 1, 1000, null, cancellationToken);
        var formulaDict = formulas.ToDictionary(f => f.Id);
        
        var animalIds = plans.Where(p => p.AnimalId.HasValue).Select(p => p.AnimalId!.Value).Distinct().ToList();
        var animals = animalIds.Count > 0 ? await _animalRepository.GetByIdsAsync(animalIds, cancellationToken) : [];
        var animalDict = animals.ToDictionary(a => a.Id);

        var basis = _rules?.Current.GroupPlanQuantityBasis ?? Farm360.Domain.BusinessRules.GroupPlanQuantityBasis.PerHead;
        var today = _clock.Today;
        var result = new List<AnimalFeedingPlanDto>(plans.Count);
        foreach (var p in plans)
        {
            Farm360.Domain.Livestock.Animal? animal = null;
            var hasAnimal = p.AnimalId.HasValue && animalDict.TryGetValue(p.AnimalId.Value, out animal);
            var animalTag = hasAnimal && animal != null ? animal.Tag.TagId
                : p.AnimalId is null ? (p.PenId.HasValue ? "Pen group" : p.ShedId.HasValue ? "Shed group" : "Batch group")
                : "Unknown";
            var animalSpecies = hasAnimal && animal != null ? animal.Species.ToString() : "Cattle";
            var animalWeight = (hasAnimal && animal?.LatestWeightKg.HasValue == true) 
                ? animal.LatestWeightKg 
                : p.TriggeredByWeightKg;

            var ruleSet = ruleSets.FirstOrDefault(r => r.Id == p.FeedingRuleSetId);
            // Direct rations read as "Extra: <feed> · <kg>"; group (shed/pen/batch) plans have no single animal.
            var ruleSetName = ruleSet is null ? "Unknown Rule Set"
                : ruleSet.Name.StartsWith(Farm360.Application.Feeding.Services.DirectFeedService.DirectRuleSetPrefix, StringComparison.Ordinal)
                    ? "Extra: " + ruleSet.Name[Farm360.Application.Feeding.Services.DirectFeedService.DirectRuleSetPrefix.Length..]
                    : ruleSet.Name;

            // Resolve rule line
            var ruleLine = p.CurrentRuleLineId.HasValue
                ? ruleSet?.Lines.FirstOrDefault(l => l.Id == p.CurrentRuleLineId.Value)
                : null;

            if (ruleLine == null && ruleSet?.Lines.Count > 0)
            {
                ruleLine = Farm360.Application.Feeding.Services.FeedingRuleMatcher.Match(ruleSet, animalWeight, null).Primary;
            }

            // Resolve formula and unit cost
            FeedFormula? formula = null;
            if (ruleLine != null && ruleLine.FormulaId != Guid.Empty)
            {
                formulaDict.TryGetValue(ruleLine.FormulaId, out formula);
            }

            var formulaName = formula?.Title ?? (ruleLine != null ? ruleLine.FeedType.ToString() : "Standard Feed");
            // Live cost per kg of the recipe (stock items priced per kg of feed).
            var unitCost = _calculator is null
                ? formula?.TotalCostPerKgBdt ?? 0m
                : Math.Round(await _calculator.FormulaCostPerKgAsync(formula, cancellationToken), 2);

            // A group plan feeds its ration to every head (PerHead basis) or its quantity already is the group's total.
            var headCount = 1;
            if (!p.AnimalId.HasValue && p.Status == FeedingPlanStatus.Active)
            {
                var members = await _animalRepository.GetActiveByScopeAcrossTenantsAsync(p.TenantId, p.BatchId, p.ShedId, p.PenId, today, cancellationToken);
                headCount = Math.Max(1, members.Count);
            }
            var heads = Farm360.Domain.Feeding.Allocation.GroupFeedQuantity.HeadMultiplier(!p.AnimalId.HasValue, headCount, basis);

            // Calculate rations
            decimal concentrateKg = 0m;
            if (p.CurrentConcentrateKgPerDay.HasValue && p.CurrentConcentrateKgPerDay.Value > 0)
            {
                concentrateKg = p.CurrentConcentrateKgPerDay.Value;
            }
            else if (ruleSet != null && ruleLine != null)
            {
                if (ruleSet.PlanType == FeedingPlanType.WeightPercentage && (animalWeight ?? 0m) > 0)
                {
                    concentrateKg = Math.Round(((animalWeight!.Value) * ruleLine.ConcentrateKgPerDay) / 100m, 2);
                }
                else
                {
                    concentrateKg = ruleLine.ConcentrateKgPerDay;
                }
            }

            decimal roughageKg = p.CurrentRoughageKgPerDay ?? ruleLine?.RoughageKgPerDay ?? 0m;
            decimal totalFeedKg = (concentrateKg + roughageKg > 0)
                ? (concentrateKg + roughageKg)
                : ((p.CurrentConcentrateKgPerDay ?? 0) + (p.CurrentRoughageKgPerDay ?? 0));

            // Daily entries and stock cover the concentrate ration only (FeedingRuleMatcher.ExpectedKg):
            // roughage is not priced with the concentrate recipe's cost.
            decimal dailyCost = Math.Round(concentrateKg * unitCost * heads, 2);

            result.Add(new AnimalFeedingPlanDto(
                p.Id,
                p.AnimalId ?? Guid.Empty,
                animalTag,
                p.FeedingRuleSetId,
                ruleSetName,
                p.StartDate,
                p.Status == FeedingPlanStatus.Cancelled ? p.EndDate : null,
                p.Status == FeedingPlanStatus.Active,
                totalFeedKg,
                concentrateKg,
                roughageKg,
                animalWeight,
                animalSpecies,
                formulaName,
                unitCost,
                dailyCost,
                new List<object>(),
                headCount,
                p.AnimalId.HasValue ? "Concentrate ration only"
                    : basis == Farm360.Domain.BusinessRules.GroupPlanQuantityBasis.PerHead
                        ? $"Concentrate ration only, {headCount} head(s)"
                        : "Concentrate ration only, whole group"));
        }

        return result;
    }
}
