using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedingEntries;

public record DailyFeedingEntryDto(
    Guid Id,
    Guid AnimalId,
    string AnimalTag,
    string? ShedName,
    string? PenName,
    Guid RuleSetId,
    DateOnly TargetDate,
    decimal ExpectedKg,
    decimal? ActualKg,
    DailyFeedingEntryStatus Status,
    string? Notes,
    DateTime? ConfirmedAtUtc,
    string? FormulaName,
    decimal? UnitCostBdt = null,
    decimal? TotalCostBdt = null,
    decimal HeadCount = 1m,
    decimal TotalKg = 0m,
    string Unit = "kg");

public sealed record GetTodayFeedingEntriesQuery(Guid? FarmId = null, DateOnly? TargetDate = null) : IRequest<IReadOnlyList<DailyFeedingEntryDto>>;

public sealed class GetTodayFeedingEntriesQueryHandler : IRequestHandler<GetTodayFeedingEntriesQuery, IReadOnlyList<DailyFeedingEntryDto>>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IShedRepository _shedRepository;
    private readonly IPenRepository _penRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly ITenantService _tenantService;
    private readonly IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly IFeedingRuleSetRepository? _ruleSetRepository;
    private readonly IFarmBusinessRulesProvider? _rules;

    public GetTodayFeedingEntriesQueryHandler(
        IDailyFeedingEntryRepository repository,
        IAnimalFeedingPlanRepository planRepository,
        IAnimalRepository animalRepository,
        IShedRepository shedRepository,
        IPenRepository penRepository,
        IFeedFormulaRepository formulaRepository,
        ITenantService tenantService,
        IAnimalFeedAllocationRepository? allocationRepository = null,
        IFeedingRuleSetRepository? ruleSetRepository = null,
        IFarmBusinessRulesProvider? rules = null)
    {
        _allocationRepository = allocationRepository;
        _ruleSetRepository = ruleSetRepository;
        _rules = rules;
        _repository = repository;
        _planRepository = planRepository;
        _animalRepository = animalRepository;
        _shedRepository = shedRepository;
        _penRepository = penRepository;
        _formulaRepository = formulaRepository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<DailyFeedingEntryDto>> Handle(GetTodayFeedingEntriesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;
        var targetDate = request.TargetDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var farmId = request.FarmId ?? Guid.Empty;
        var entries = await _repository.GetEntriesByDateAsync(tenantId, farmId, targetDate, cancellationToken);

        if (entries.Count == 0)
            return Array.Empty<DailyFeedingEntryDto>();

        var planIds = entries.Select(e => e.FeedingPlanId).Distinct().ToList();
        var plans = await _planRepository.GetByIdsAsync(planIds, cancellationToken);
        var planDict = plans.ToDictionary(p => p.Id);

        var animalIds = plans.Where(p => p.AnimalId.HasValue).Select(p => p.AnimalId!.Value).Distinct().ToList();
        var animals = animalIds.Count > 0 ? await _animalRepository.GetByIdsAsync(animalIds, cancellationToken) : [];
        var animalDict = animals.ToDictionary(a => a.Id);

        var farmIds = entries.Select(e => e.FarmId).Distinct().ToList();
        if (farmId != Guid.Empty && !farmIds.Contains(farmId)) farmIds.Add(farmId);

        var sheds = new List<Farm360.Domain.Farms.Shed>();
        foreach (var fid in farmIds)
        {
            var sList = await _shedRepository.GetAllByFarmAsync(tenantId, fid, cancellationToken);
            sheds.AddRange(sList);
        }
        var shedDict = sheds.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().ShedName);

        var penDict = new Dictionary<Guid, string>();
        foreach (var shed in sheds)
        {
            var pens = await _penRepository.GetAllByShedAsync(tenantId, shed.Id, cancellationToken);
            foreach (var pen in pens)
            {
                penDict[pen.Id] = pen.PenName;
            }
        }

        var formulas = await _formulaRepository.GetListAsync(tenantId, 1, 1000, null, cancellationToken);
        var formulaDict = formulas.ToDictionary(f => f.Id, f => f.Title);

        // Heads each entry's quantity covers (group plans, by the quantity basis) and its unit (a liquid
        // fed as it is is recorded in ml, not kg), so the page can total kg and cost correctly.
        var allocationsByEntry = new Dictionary<Guid, List<Farm360.Domain.Feeding.AnimalFeedAllocation>>();
        if (_allocationRepository is not null)
        {
            foreach (var fid in farmIds.Where(f => f != Guid.Empty))
            {
                foreach (var a in await _allocationRepository.GetByFarmAsync(fid, targetDate, targetDate, cancellationToken: cancellationToken))
                {
                    if (!allocationsByEntry.TryGetValue(a.DailyFeedingEntryId, out var list))
                        allocationsByEntry[a.DailyFeedingEntryId] = list = [];
                    list.Add(a);
                }
            }
        }
        var ruleSetNames = _ruleSetRepository is null
            ? new Dictionary<Guid, string>()
            : (await _ruleSetRepository.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id, r => r.Name);
        var basis = _rules?.Current.GroupPlanQuantityBasis ?? Farm360.Domain.BusinessRules.GroupPlanQuantityBasis.PerHead;

        return entries.Select(e =>
        {
            planDict.TryGetValue(e.FeedingPlanId, out var plan);
            var heads = Farm360.Application.Feeding.Services.FeedEntryHeads.Multiplier(plan,
                allocationsByEntry.TryGetValue(e.Id, out var rows) ? rows : [], basis);
            var unit = plan is not null && ruleSetNames.TryGetValue(plan.FeedingRuleSetId, out var rsName)
                ? Farm360.Application.Feeding.Services.DirectFeedService.UnitFromName(rsName)
                : "kg";
            var animalId = plan?.AnimalId ?? Guid.Empty;
            animalDict.TryGetValue(animalId, out var animal);
            var animalTag = animal?.Tag?.TagId ?? (animalId != Guid.Empty ? "Tag N/A" : "General Herd");

            var resolvedShedId = e.ShedId ?? plan?.ShedId ?? animal?.CurrentMovement?.ShedId;
            var resolvedPenId = e.PenId ?? plan?.PenId ?? animal?.CurrentMovement?.PenId;

            var shedName = resolvedShedId.HasValue && shedDict.TryGetValue(resolvedShedId.Value, out var sName) ? sName : null;
            var penName = resolvedPenId.HasValue && penDict.TryGetValue(resolvedPenId.Value, out var pName) ? pName : null;
            var formulaName = formulaDict.TryGetValue(e.FormulaId, out var fName) ? fName : "Base Ration";

            return new DailyFeedingEntryDto(
                e.Id,
                animalId,
                animalTag,
                shedName,
                penName,
                plan?.FeedingRuleSetId ?? Guid.Empty,
                e.EntryDate,
                e.ExpectedKg,
                e.ActualKg,
                e.Status,
                e.AdjustmentReason,
                null,
                formulaName,
                e.UnitCostAtConsumptionBdt,
                e.TotalCostBdt,
                heads,
                Math.Round((e.ActualKg ?? e.ExpectedKg) * heads, 4),
                unit
            );
        }).ToList();
    }
}
