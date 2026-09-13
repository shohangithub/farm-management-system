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
    IReadOnlyList<object> Exclusions);

internal sealed class GetFeedingPlansQueryHandler : IRequestHandler<GetFeedingPlansQuery, IReadOnlyList<AnimalFeedingPlanDto>>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly Farm360.Domain.Livestock.Repositories.IAnimalRepository _animalRepository;
    private readonly ITenantService _tenantService;

    public GetFeedingPlansQueryHandler(
        IAnimalFeedingPlanRepository repository, 
        IFeedingRuleSetRepository ruleSetRepository, 
        IFeedFormulaRepository formulaRepository,
        Farm360.Domain.Livestock.Repositories.IAnimalRepository animalRepository,
        ITenantService tenantService)
    {
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

        return plans.Select(p =>
        {
            Farm360.Domain.Livestock.Animal? animal = null;
            var hasAnimal = p.AnimalId.HasValue && animalDict.TryGetValue(p.AnimalId.Value, out animal);
            var animalTag = hasAnimal && animal != null ? animal.Tag.TagId : "Unknown";
            var animalSpecies = hasAnimal && animal != null ? animal.Species.ToString() : "Cattle";
            var animalWeight = (hasAnimal && animal?.LatestWeightKg.HasValue == true) 
                ? animal.LatestWeightKg 
                : p.TriggeredByWeightKg;

            var ruleSet = ruleSets.FirstOrDefault(r => r.Id == p.FeedingRuleSetId);
            var ruleSetName = ruleSet?.Name ?? "Unknown Rule Set";

            // Resolve rule line
            var ruleLine = p.CurrentRuleLineId.HasValue
                ? ruleSet?.Lines.FirstOrDefault(l => l.Id == p.CurrentRuleLineId.Value)
                : null;

            if (ruleLine == null && ruleSet?.Lines.Count > 0)
            {
                var w = animalWeight ?? 0m;
                ruleLine = ruleSet.Lines.FirstOrDefault(l => w >= l.WeightFromKg && w < l.WeightToKg) 
                           ?? ruleSet.Lines.OrderBy(l => l.WeightFromKg).FirstOrDefault();
            }

            // Resolve formula and unit cost
            FeedFormula? formula = null;
            if (ruleLine != null && ruleLine.FormulaId != Guid.Empty)
            {
                formulaDict.TryGetValue(ruleLine.FormulaId, out formula);
            }

            var formulaName = formula?.Title ?? (ruleLine != null ? ruleLine.FeedType.ToString() : "Standard Feed");
            var unitCost = formula?.TotalCostPerKgBdt ?? 0m;

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

            decimal dailyCost = Math.Round(totalFeedKg * unitCost, 2);

            return new AnimalFeedingPlanDto(
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
                new List<object>()
            );
        }).ToList();
    }
}
