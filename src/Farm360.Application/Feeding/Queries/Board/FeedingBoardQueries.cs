using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Board;

// ── DTOs ───────────────────────────────────────────────────────────────────

/// <param name="Kind">Chart (rule set), Extra (direct ration) or Group (shed / pen / batch plan).</param>
/// <param name="Fit">InRange, BelowRange, AboveRange or Unknown — whether the animal sits inside the chart's ranges.</param>
public sealed record RationItemDto(
    Guid? PlanId,
    Guid RuleSetId,
    string Name,
    string Kind,
    string PlanType,
    string FeedName,
    decimal KgPerDay,
    decimal CostPerDayBdt,
    string Fit,
    bool IsPaused,
    DateOnly? PausedUntil);

public sealed record BoardAnimalDto(
    Guid AnimalId,
    string Tag,
    string Species,
    Guid? ShedId,
    string? ShedName,
    Guid? PenId,
    string? PenName,
    Guid? BatchId,
    string? BatchName,
    decimal? WeightKg,
    int? AgeDays,
    IReadOnlyList<RationItemDto> Items,
    decimal TotalKgPerDay,
    decimal TotalCostPerDayBdt);

public sealed record BoardSummaryDto(
    int Animals,
    int Fed,
    int Unfed,
    int WithExtras,
    int Paused,
    decimal TotalKgPerDay,
    decimal TotalCostPerDayBdt,
    int ActiveLegacySchedules);

public sealed record FeedingBoardDto(Guid FarmId, DateOnly Date, BoardSummaryDto Summary, IReadOnlyList<BoardAnimalDto> Animals);

public sealed record PreviewAnimalDto(
    Guid AnimalId,
    string Tag,
    decimal? WeightKg,
    int? AgeDays,
    IReadOnlyList<RationItemDto> Items,
    decimal TotalKgPerDay,
    decimal TotalCostPerDayBdt,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> WillEnd,
    IReadOnlyList<string> AlreadyOn);

public sealed record RationPreviewDto(
    IReadOnlyList<PreviewAnimalDto> Animals,
    int GroupHeadCount,
    decimal TotalKgPerDay,
    decimal TotalCostPerDayBdt);

// ── Shared calculation ─────────────────────────────────────────────────────

/// <summary>What a rule set gives one animal today, in kg and taka — shared by the board and the preview.</summary>
internal sealed class RationCalculator
{
    public const string DirectPrefix = DirectFeedService.DirectRuleSetPrefix;

    private readonly Dictionary<Guid, FeedFormula> _formulas;

    public RationCalculator(IEnumerable<FeedFormula> formulas)
    {
        _formulas = formulas.GroupBy(f => f.Id).ToDictionary(g => g.Key, g => g.First());
    }

    public static string KindOf(FeedingRuleSet ruleSet, AnimalFeedingPlan? plan) =>
        plan is { AnimalId: null } ? "Group"
        : ruleSet.Name.StartsWith(DirectPrefix, StringComparison.Ordinal) ? "Extra"
        : "Chart";

    public static string DisplayName(FeedingRuleSet ruleSet) =>
        ruleSet.Name.StartsWith(DirectPrefix, StringComparison.Ordinal) ? ruleSet.Name[DirectPrefix.Length..] : ruleSet.Name;

    public RationItemDto Item(FeedingRuleSet ruleSet, AnimalFeedingPlan? plan, decimal? weightKg, int? ageDays, DateOnly day)
    {
        var match = FeedingRuleMatcher.Match(ruleSet, weightKg, ageDays);
        decimal kg = 0m, cost = 0m;
        var feeds = new List<string>();
        foreach (var line in match.Lines)
        {
            var lineKg = FeedingRuleMatcher.ExpectedKg(ruleSet, line, weightKg ?? 0m);
            kg += lineKg;
            if (_formulas.TryGetValue(line.FormulaId, out var formula))
            {
                cost += lineKg * formula.TotalCostPerKgBdt;
                feeds.Add(formula.Title.Replace(DirectFeedService.DirectItemSuffix, string.Empty, StringComparison.Ordinal));
            }
            else
            {
                feeds.Add(line.FeedType.ToString());
            }
        }

        var paused = plan?.IsPausedOn(day) ?? false;
        return new RationItemDto(
            plan?.Id,
            ruleSet.Id,
            DisplayName(ruleSet),
            KindOf(ruleSet, plan),
            ruleSet.PlanType.ToString(),
            string.Join(" + ", feeds.Distinct()),
            Math.Round(kg, 3),
            Math.Round(cost, 2),
            match.Fit.ToString(),
            paused,
            paused ? plan!.PausedUntil(day) : null);
    }
}

// ── Board ──────────────────────────────────────────────────────────────────

/// <summary>"Who eats what": every animal on the farm with every ration feeding it today.</summary>
public sealed record GetFeedingBoardQuery(Guid FarmId) : IRequest<FeedingBoardDto>;

public sealed class GetFeedingBoardQueryHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IFeedFormulaRepository formulas,
    IFeedingScheduleRepository schedules,
    IShedRepository sheds,
    IPenRepository pens,
    IAnimalBatchRepository batches,
    ITenantService tenant) : IRequestHandler<GetFeedingBoardQuery, FeedingBoardDto>
{
    public async Task<FeedingBoardDto> Handle(GetFeedingBoardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var herd = (await animals.GetAllByFarmAsync(request.FarmId, cancellationToken))
            .Where(a => a.Status is AnimalStatus.Active or AnimalStatus.Quarantined)
            .OrderBy(a => a.Tag.TagId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var locations = await animals.GetCurrentLocationsByFarmAsync(request.FarmId, cancellationToken);
        var activePlans = await plans.GetActivePlansByFarmAsync(tenantId, request.FarmId, cancellationToken);
        var sets = (await ruleSets.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id);
        var calc = new RationCalculator(await formulas.GetListAsync(tenantId, 1, 2000, null, cancellationToken));

        var names = await LocationNames.LoadAsync(tenantId, request.FarmId, herd, sheds, pens, batches, cancellationToken);

        var rows = new List<BoardAnimalDto>(herd.Count);
        foreach (var animal in herd)
        {
            locations.TryGetValue(animal.Id, out var place);
            var age = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, today);
            var items = new List<RationItemDto>();

            foreach (var plan in activePlans.Where(p => p.StartDate <= today && (p.EndDate is null || p.EndDate >= today)))
            {
                if (!sets.TryGetValue(plan.FeedingRuleSetId, out var set) || !set.IsActive)
                    continue;

                if (plan.AnimalId == animal.Id)
                {
                    items.Add(calc.Item(set, plan, animal.LatestWeightKg, age, today));
                }
                else if (plan.AnimalId is null && Covers(plan, animal, place.PenId, place.ShedId))
                {
                    // Group plans are fed per head at the plan's own (group) weight, as the daily job does.
                    items.Add(calc.Item(set, plan, plan.TriggeredByWeightKg, null, today));
                }
            }

            var feeding = items.Where(i => !i.IsPaused).ToList();
            rows.Add(new BoardAnimalDto(
                animal.Id,
                animal.Tag.TagId,
                animal.Species.ToString(),
                place.ShedId,
                names.Shed(place.ShedId),
                place.PenId,
                names.Pen(place.PenId),
                animal.BatchId,
                names.Batch(animal.BatchId),
                animal.LatestWeightKg,
                age,
                items.OrderBy(i => i.Kind == "Chart" ? 0 : i.Kind == "Group" ? 1 : 2).ToList(),
                Math.Round(feeding.Sum(i => i.KgPerDay), 3),
                Math.Round(feeding.Sum(i => i.CostPerDayBdt), 2)));
        }

        var legacy = (await schedules.GetListByFarmAsync(tenantId, request.FarmId, cancellationToken))
            .Count(s => s.IsActive && (s.EndDate is null || s.EndDate >= today));

        var summary = new BoardSummaryDto(
            rows.Count,
            rows.Count(r => r.Items.Any(i => !i.IsPaused)),
            rows.Count(r => r.Items.All(i => i.IsPaused)),
            rows.Count(r => r.Items.Any(i => i.Kind == "Extra")),
            rows.Count(r => r.Items.Any(i => i.IsPaused)),
            Math.Round(rows.Sum(r => r.TotalKgPerDay), 2),
            Math.Round(rows.Sum(r => r.TotalCostPerDayBdt), 2),
            legacy);

        return new FeedingBoardDto(request.FarmId, today, summary, rows);
    }

    /// <summary>Same scope rule as the allocation service: the most specific of pen, shed, batch.</summary>
    internal static bool Covers(AnimalFeedingPlan plan, Animal animal, Guid? penId, Guid? shedId)
    {
        if (plan.PenId is { } pen) return penId == pen && (plan.BatchId is null || animal.BatchId == plan.BatchId);
        if (plan.ShedId is { } shed) return shedId == shed && (plan.BatchId is null || animal.BatchId == plan.BatchId);
        return plan.BatchId is { } batch && animal.BatchId == batch;
    }
}

// ── Preview ────────────────────────────────────────────────────────────────

/// <summary>
/// What each animal will get if the ration is saved — before anything is created. Shows the
/// chart range each animal falls in, kg and taka a day, plans a switch would end and duplicates.
/// </summary>
public sealed record PreviewRationQuery(
    Guid FarmId,
    IReadOnlyList<Guid>? AnimalIds,
    IReadOnlyList<Guid>? RuleSetIds,
    IReadOnlyList<DirectRation>? DirectRations,
    Guid? ShedId = null,
    Guid? PenId = null,
    Guid? BatchId = null,
    bool ReplaceExistingCharts = false) : IRequest<RationPreviewDto>;

public sealed class PreviewRationQueryHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IFeedFormulaRepository formulas,
    IInventoryItemRepository inventory,
    ITenantService tenant) : IRequestHandler<PreviewRationQuery, RationPreviewDto>
{
    public async Task<RationPreviewDto> Handle(PreviewRationQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sets = (await ruleSets.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id);
        var formulaList = (await formulas.GetListAsync(tenantId, 1, 2000, null, cancellationToken)).ToList();
        var chosen = (request.RuleSetIds ?? []).Distinct().Where(sets.ContainsKey).Select(id => sets[id]).ToList();

        // Direct rations are previewed as one-line fixed rule sets without saving anything.
        var directPreviews = new List<FeedingRuleSet>();
        var standIns = new List<FeedFormula>();
        foreach (var ration in request.DirectRations ?? [])
        {
            var (formulaId, title, costPerKg) = await DescribeAsync(ration, formulaList, cancellationToken);
            var lineFormulaId = formulaId ?? Guid.NewGuid();
            var temp = new FeedingRuleSet(Guid.NewGuid(), tenantId, $"{RationCalculator.DirectPrefix}{title} · {ration.KgPerDay:0.###} kg/day",
                TargetAnimalType.Cattle, FeedingPurpose.Growth, FeedingPlanType.FixedQuantity);
            temp.AddRuleLine(null, null, null, null, ration.FeedType ?? FeedCategory.Concentrate, ration.KgPerDay, lineFormulaId);
            directPreviews.Add(temp);
            if (formulaId is null)
            {
                // A stock item not yet wrapped in a formula: price it from the item.
                standIns.Add(PricedStandIn(lineFormulaId, title, costPerKg, tenantId));
            }
        }

        var calc = new RationCalculator(formulaList.Concat(standIns));

        var herd = (await animals.GetAllByFarmAsync(request.FarmId, cancellationToken))
            .Where(a => a.Status is AnimalStatus.Active or AnimalStatus.Quarantined)
            .ToList();
        var locations = await animals.GetCurrentLocationsByFarmAsync(request.FarmId, cancellationToken);
        var active = await plans.GetActivePlansByFarmAsync(tenantId, request.FarmId, cancellationToken);

        var isGroup = (request.AnimalIds?.Count ?? 0) == 0;
        List<Animal> subjects;
        if (!isGroup)
        {
            subjects = herd.Where(a => request.AnimalIds!.Contains(a.Id)).ToList();
        }
        else if ((request.ShedId ?? request.PenId ?? request.BatchId) is null)
        {
            subjects = [];
        }
        else
        {
            var probe = new AnimalFeedingPlan(Guid.NewGuid(), tenantId, request.FarmId, Guid.Empty, FeedingPlanType.FixedQuantity,
                today, null, null, request.BatchId, request.ShedId, request.PenId);
            subjects = herd.Where(a =>
            {
                locations.TryGetValue(a.Id, out var p);
                return GetFeedingBoardQueryHandler.Covers(probe, a, p.PenId, p.ShedId);
            }).ToList();
        }

        var rows = new List<PreviewAnimalDto>();
        foreach (var animal in subjects.OrderBy(a => a.Tag.TagId, StringComparer.OrdinalIgnoreCase))
        {
            var age = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, today);
            // A group plan is fed at the group's weight (unknown), so preview it the same way.
            var weight = isGroup ? null : animal.LatestWeightKg;
            var items = chosen.Concat(directPreviews).Select(s => calc.Item(s, null, weight, isGroup ? null : age, today)).ToList();

            var mine = active.Where(p => p.AnimalId == animal.Id).ToList();
            var alreadyOn = chosen.Where(s => mine.Any(p => p.FeedingRuleSetId == s.Id)).Select(s => s.Name).ToList();
            var willEnd = request.ReplaceExistingCharts && chosen.Count > 0
                ? mine.Where(p => sets.TryGetValue(p.FeedingRuleSetId, out var s)
                                  && RationCalculator.KindOf(s, p) == "Chart"
                                  && chosen.All(c => c.Id != s.Id))
                      .Select(p => sets[p.FeedingRuleSetId].Name).ToList()
                : [];

            var warnings = new List<string>();
            if (!isGroup && animal.LatestWeightKg is null or 0 && chosen.Any(s => s.PlanType is FeedingPlanType.WeightQuantity or FeedingPlanType.WeightPercentage))
                warnings.Add("No weight recorded — the lowest range is used until it is weighed.");
            foreach (var item in items.Where(i => i.Fit is "BelowRange" or "AboveRange"))
                warnings.Add($"{item.Name}: {(item.Fit == "BelowRange" ? "below" : "above")} the chart's ranges — the nearest range is used.");
            if (!request.ReplaceExistingCharts && chosen.Count > 0)
            {
                var otherCharts = mine.Where(p => sets.TryGetValue(p.FeedingRuleSetId, out var s) && RationCalculator.KindOf(s, p) == "Chart"
                                                  && chosen.All(c => c.Id != s.Id)).ToList();
                if (otherCharts.Count > 0)
                    warnings.Add($"Already follows {string.Join(", ", otherCharts.Select(p => sets[p.FeedingRuleSetId].Name))} — both will be fed. Tick \"Switch\" to replace it.");
            }

            var counted = items.Where(i => !alreadyOn.Contains(i.Name)).ToList();
            rows.Add(new PreviewAnimalDto(animal.Id, animal.Tag.TagId, animal.LatestWeightKg, age, items,
                Math.Round(counted.Sum(i => i.KgPerDay), 3), Math.Round(counted.Sum(i => i.CostPerDayBdt), 2),
                warnings, willEnd, alreadyOn));
        }

        return new RationPreviewDto(rows, isGroup ? rows.Count : 0,
            Math.Round(rows.Sum(r => r.TotalKgPerDay), 2), Math.Round(rows.Sum(r => r.TotalCostPerDayBdt), 2));
    }

    private async Task<(Guid? FormulaId, string Title, decimal CostPerKg)> DescribeAsync(
        DirectRation ration, IReadOnlyList<FeedFormula> formulaList, CancellationToken ct)
    {
        if (ration.FormulaId is { } fid)
        {
            var f = formulaList.FirstOrDefault(x => x.Id == fid);
            return (fid, f?.Title ?? "Formula", f?.TotalCostPerKgBdt ?? 0m);
        }

        if (ration.InventoryItemId is { } iid)
        {
            var item = await inventory.GetByIdAsync(iid, ct);
            return (null, item?.Name ?? "Stock item", item?.WeightedAverageCostBdt ?? 0m);
        }

        return (null, "—", 0m);
    }

    private static FeedFormula PricedStandIn(Guid id, string title, decimal costPerKg, Guid tenantId)
    {
        // Never saved: lets the calculator price an item that has no direct-item formula yet.
        var f = new FeedFormula(id, tenantId, title, TargetAnimalType.Cattle);
        f.AddIngredient(Guid.NewGuid(), 100m, costPerKg, Domain.Feeding.ValueObjects.NutritionalProfile.Empty);
        return f;
    }
}

// ── Shared look-ups ────────────────────────────────────────────────────────

internal sealed class LocationNames
{
    private readonly Dictionary<Guid, string> _sheds = [];
    private readonly Dictionary<Guid, string> _pens = [];
    private readonly Dictionary<Guid, string> _batches = [];

    public string? Shed(Guid? id) => id is { } v && _sheds.TryGetValue(v, out var n) ? n : null;
    public string? Pen(Guid? id) => id is { } v && _pens.TryGetValue(v, out var n) ? n : null;
    public string? Batch(Guid? id) => id is { } v && _batches.TryGetValue(v, out var n) ? n : null;

    public static async Task<LocationNames> LoadAsync(
        Guid tenantId, Guid farmId, IEnumerable<Animal> herd,
        IShedRepository sheds, IPenRepository pens, IAnimalBatchRepository batches, CancellationToken ct)
    {
        var names = new LocationNames();
        foreach (var shed in await sheds.GetAllByFarmAsync(tenantId, farmId, ct))
        {
            names._sheds[shed.Id] = shed.ShedNumber;
            foreach (var pen in await pens.GetAllByShedAsync(tenantId, shed.Id, ct))
                names._pens[pen.Id] = string.IsNullOrWhiteSpace(pen.PenName) ? pen.PenNumber : pen.PenName;
        }

        foreach (var batchId in herd.Where(a => a.BatchId.HasValue).Select(a => a.BatchId!.Value).Distinct())
        {
            var batch = await batches.GetByIdAsync(batchId, ct);
            if (batch is not null) names._batches[batchId] = batch.Name;
        }

        return names;
    }
}
