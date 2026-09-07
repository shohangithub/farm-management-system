using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedingEntries;

public record AnimalDailyFeedingLogDto(
    Guid Id,
    DateOnly EntryDate,
    Guid FormulaId,
    string FormulaName,
    decimal ExpectedKg,
    decimal? ActualKg,
    decimal? UnitCostAtConsumptionBdt,
    decimal? TotalCostBdt,
    DailyFeedingEntryStatus Status,
    string? AdjustmentReason,
    string? RuleSetName,
    string? ShedName,
    string? PenName);

public record AnimalFeedingPlanBriefDto(
    Guid PlanId,
    Guid RuleSetId,
    string RuleSetName,
    FeedingPlanType PlanType,
    DateOnly StartDate,
    DateOnly? EndDate,
    FeedingPlanStatus Status,
    decimal? CurrentConcentrateKgPerDay,
    decimal? CurrentRoughageKgPerDay,
    decimal? TriggeredByWeightKg);

public record AnimalFeedingSummaryDto(
    Guid AnimalId,
    decimal TotalFeedCostBdt,
    decimal TotalFeedConsumedKg,
    decimal AverageCostPerKgBdt,
    int TotalEntriesCount,
    int ConfirmedEntriesCount,
    int PendingEntriesCount,
    int SkippedEntriesCount,
    AnimalFeedingPlanBriefDto? ActivePlan,
    IReadOnlyList<AnimalFeedingPlanBriefDto> Plans,
    IReadOnlyList<AnimalDailyFeedingLogDto> Entries);

public record GetAnimalFeedingSummaryQuery(Guid AnimalId) : IRequest<AnimalFeedingSummaryDto>;

public class GetAnimalFeedingSummaryQueryValidator : AbstractValidator<GetAnimalFeedingSummaryQuery>
{
    public GetAnimalFeedingSummaryQueryValidator()
    {
        RuleFor(x => x.AnimalId).NotEmpty();
    }
}

public class GetAnimalFeedingSummaryQueryHandler : IRequestHandler<GetAnimalFeedingSummaryQuery, AnimalFeedingSummaryDto>
{
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IShedRepository _shedRepository;
    private readonly IPenRepository _penRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly ITenantService _tenantService;

    public GetAnimalFeedingSummaryQueryHandler(
        IDailyFeedingEntryRepository entryRepository,
        IAnimalFeedingPlanRepository planRepository,
        IFeedingRuleSetRepository ruleSetRepository,
        IFeedFormulaRepository formulaRepository,
        IShedRepository shedRepository,
        IPenRepository penRepository,
        IAnimalRepository animalRepository,
        ITenantService tenantService)
    {
        _entryRepository = entryRepository;
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _formulaRepository = formulaRepository;
        _shedRepository = shedRepository;
        _penRepository = penRepository;
        _animalRepository = animalRepository;
        _tenantService = tenantService;
    }

    public async Task<AnimalFeedingSummaryDto> Handle(GetAnimalFeedingSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;
        var animal = await _animalRepository.GetByIdAsync(request.AnimalId, cancellationToken);
        var farmId = animal?.FarmId ?? Guid.Empty;

        // Fetch plans for this animal
        var plans = await _planRepository.GetAllPlansForAnimalAsync(tenantId, request.AnimalId, cancellationToken);
        var planDict = plans.ToDictionary(p => p.Id);

        // Fetch rule sets for display names
        var allRuleSets = await _ruleSetRepository.GetAllAsync(cancellationToken);
        var ruleSetDict = allRuleSets.ToDictionary(r => r.Id, r => r.Name);

        // Fetch formulas for formula title
        var formulas = await _formulaRepository.GetListAsync(tenantId, 1, 1000, null, cancellationToken);
        var formulaDict = formulas.ToDictionary(f => f.Id, f => f.Title);

        // Fetch sheds & pens if farm exists
        var shedDict = new Dictionary<Guid, string>();
        var penDict = new Dictionary<Guid, string>();
        if (farmId != Guid.Empty)
        {
            var sheds = await _shedRepository.GetAllByFarmAsync(tenantId, farmId, cancellationToken);
            foreach (var s in sheds)
            {
                shedDict[s.Id] = s.ShedName;
                var pens = await _penRepository.GetAllByShedAsync(tenantId, s.Id, cancellationToken);
                foreach (var p in pens)
                {
                    penDict[p.Id] = p.PenName;
                }
            }
        }

        // Map plan briefs
        var planBriefs = plans.Select(p =>
        {
            var rName = ruleSetDict.TryGetValue(p.FeedingRuleSetId, out var name) ? name : "Standard Rule Set";
            return new AnimalFeedingPlanBriefDto(
                p.Id,
                p.FeedingRuleSetId,
                rName,
                p.PlanType,
                p.StartDate,
                p.EndDate,
                p.Status,
                p.CurrentConcentrateKgPerDay,
                p.CurrentRoughageKgPerDay,
                p.TriggeredByWeightKg);
        }).ToList();

        var activePlanBrief = planBriefs.FirstOrDefault(p => p.Status == FeedingPlanStatus.Active);

        // Fetch daily entries for animal
        var entries = await _entryRepository.GetEntriesByAnimalIdAsync(tenantId, request.AnimalId, cancellationToken);

        decimal totalFeedCost = 0m;
        decimal totalFeedConsumedKg = 0m;
        int confirmedCount = 0;
        int pendingCount = 0;
        int skippedCount = 0;

        var mappedEntries = entries.Select(e =>
        {
            planDict.TryGetValue(e.FeedingPlanId, out var plan);
            var ruleSetName = plan != null && ruleSetDict.TryGetValue(plan.FeedingRuleSetId, out var rsName) ? rsName : null;
            var formulaName = formulaDict.TryGetValue(e.FormulaId, out var fName) ? fName : "Base Ration";

            var resolvedShedId = e.ShedId ?? plan?.ShedId ?? animal?.CurrentMovement?.ShedId;
            var resolvedPenId = e.PenId ?? plan?.PenId ?? animal?.CurrentMovement?.PenId;

            var shedName = resolvedShedId.HasValue && shedDict.TryGetValue(resolvedShedId.Value, out var sName) ? sName : null;
            var penName = resolvedPenId.HasValue && penDict.TryGetValue(resolvedPenId.Value, out var pName) ? pName : null;

            if (e.Status == DailyFeedingEntryStatus.Confirmed || e.Status == DailyFeedingEntryStatus.Adjusted)
            {
                confirmedCount++;
                if (e.TotalCostBdt.HasValue)
                {
                    totalFeedCost += e.TotalCostBdt.Value;
                }
                totalFeedConsumedKg += e.ActualKg ?? e.ExpectedKg;
            }
            else if (e.Status == DailyFeedingEntryStatus.Pending)
            {
                pendingCount++;
            }
            else if (e.Status == DailyFeedingEntryStatus.Skipped)
            {
                skippedCount++;
            }

            return new AnimalDailyFeedingLogDto(
                e.Id,
                e.EntryDate,
                e.FormulaId,
                formulaName,
                e.ExpectedKg,
                e.ActualKg,
                e.UnitCostAtConsumptionBdt,
                e.TotalCostBdt,
                e.Status,
                e.AdjustmentReason,
                ruleSetName,
                shedName,
                penName);
        }).ToList();

        decimal avgCostPerKg = totalFeedConsumedKg > 0 
            ? Math.Round(totalFeedCost / totalFeedConsumedKg, 2) 
            : 0m;

        return new AnimalFeedingSummaryDto(
            request.AnimalId,
            Math.Round(totalFeedCost, 2),
            Math.Round(totalFeedConsumedKg, 2),
            avgCostPerKg,
            entries.Count,
            confirmedCount,
            pendingCount,
            skippedCount,
            activePlanBrief,
            planBriefs,
            mappedEntries);
    }
}
