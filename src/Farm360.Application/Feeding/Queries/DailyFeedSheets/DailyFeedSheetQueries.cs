using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedSheets;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedSheets;

/// <summary>One feed an animal got (or is planned to get) on the sheet's day.</summary>
/// <param name="Source">"Daily sheet", "Manual" or "Plan".</param>
/// <param name="CanRemove">False for a whole-group plan entry, which can't be removed for one animal.</param>
public sealed record FeedSheetFedDto(
    Guid EntryId,
    string Feed,
    decimal Quantity,
    string Unit,
    decimal CostBdt,
    bool Confirmed,
    string Source,
    bool CanRemove);

public sealed record FeedSheetAnimalDto(
    Guid AnimalId,
    string TagId,
    string Breed,
    string Sex,
    decimal? WeightKg,
    Guid? BatchId,
    string? BatchName,
    Guid? ShedId,
    string? ShedName,
    Guid? PenId,
    string? PenName,
    IReadOnlyList<string> ActivePlans,
    bool HasOwnPlan,
    IReadOnlyList<FeedSheetFedDto> Fed);

public sealed record DailyFeedSheetDto(DateOnly Date, IReadOnlyList<FeedSheetAnimalDto> Animals);

/// <summary>
/// The animals on a farm on one day, with where they are, which plans feed them, and what they have
/// been fed so far that day. This is everything the daily feed sheet screen needs to show.
/// </summary>
public sealed record GetDailyFeedSheetQuery(Guid FarmId, DateOnly Date) : IRequest<DailyFeedSheetDto>;

public sealed class GetDailyFeedSheetQueryHandler(
    IAnimalRepository animals,
    IBreedRepository breeds,
    IAnimalBatchRepository batches,
    IShedRepository sheds,
    IPenRepository pens,
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IDailyFeedingEntryRepository entries,
    IAnimalFeedAllocationRepository allocations,
    IFeedFormulaRepository formulas,
    ITenantService tenant) : IRequestHandler<GetDailyFeedSheetQuery, DailyFeedSheetDto>
{
    public async Task<DailyFeedSheetDto> Handle(GetDailyFeedSheetQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;
        var date = request.Date;

        var herd = (await animals.GetPresentDuringPeriodAsync(request.FarmId, date, date, cancellationToken))
            .Where(a => AnimalFeedingPresence.IsFedOn(a, date))
            .ToList();
        if (herd.Count == 0)
            return new DailyFeedSheetDto(date, []);

        var breedNames = (await breeds.GetAllAsync(cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var locations = await animals.GetCurrentLocationsByFarmAsync(request.FarmId, cancellationToken);

        var shedNames = (await sheds.GetAllByFarmAsync(tenantId, request.FarmId, cancellationToken))
            .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.ShedName) ? s.ShedNumber : s.ShedName);
        var penNames = new Dictionary<Guid, string>();
        foreach (var shedId in shedNames.Keys)
            foreach (var pen in await pens.GetAllByShedAsync(tenantId, shedId, cancellationToken))
                penNames[pen.Id] = string.IsNullOrWhiteSpace(pen.PenName) ? pen.PenNumber : pen.PenName;

        var batchNames = new Dictionary<Guid, string>();
        foreach (var batchId in herd.Where(a => a.BatchId.HasValue).Select(a => a.BatchId!.Value).Distinct())
            if (await batches.GetByIdAsync(batchId, cancellationToken) is { } batch)
                batchNames[batchId] = batch.Name;

        // Which plans feed each animal.
        var ruleSetNames = (await ruleSets.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id, r => r.Name);
        var planNames = herd.ToDictionary(a => a.Id, _ => new List<string>());
        var ownPlan = new HashSet<Guid>();
        foreach (var plan in await plans.GetActivePlansByFarmAsync(tenantId, request.FarmId, cancellationToken))
        {
            var name = ruleSetNames.GetValueOrDefault(plan.FeedingRuleSetId) ?? "Feeding plan";
            if (plan.AnimalId is { } animalId)
            {
                if (planNames.TryGetValue(animalId, out var list))
                {
                    list.Add(name);
                    ownPlan.Add(animalId);
                }

                continue;
            }

            foreach (var member in await animals.GetActiveByScopeAcrossTenantsAsync(tenantId, plan.BatchId, plan.ShedId, plan.PenId, date, cancellationToken))
                if (planNames.TryGetValue(member.Id, out var list))
                    list.Add($"{name} (group)");
        }

        // What each animal got that day, from its share of each entry.
        var dayEntries = (await entries.GetEntriesByDateAsync(tenantId, request.FarmId, date, cancellationToken))
            .Where(e => e.Status is DailyFeedingEntryStatus.Pending or DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted)
            .ToDictionary(e => e.Id);
        var shares = (await allocations.GetByFarmAsync(request.FarmId, date, date, cancellationToken: cancellationToken))
            .Where(s => dayEntries.ContainsKey(s.DailyFeedingEntryId))
            .ToList();
        var entryPlans = (await plans.GetByIdsAsync(dayEntries.Values.Select(e => e.FeedingPlanId).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.Id);
        var formulaNames = new Dictionary<Guid, string>();
        foreach (var formulaId in shares.Select(s => s.FormulaId).Distinct())
            formulaNames[formulaId] = FeedName((await formulas.GetByIdAsync(formulaId, cancellationToken))?.Title);

        var fedByAnimal = shares
            .GroupBy(s => s.AnimalId)
            .ToDictionary(g => g.Key, g => g.Select(s =>
            {
                var entry = dayEntries[s.DailyFeedingEntryId];
                entryPlans.TryGetValue(entry.FeedingPlanId, out var plan);
                var unit = plan is null ? "kg" : DirectFeedService.UnitFromName(ruleSetNames.GetValueOrDefault(plan.FeedingRuleSetId) ?? string.Empty);
                var confirmed = entry.Status != DailyFeedingEntryStatus.Pending;
                return new FeedSheetFedDto(
                    entry.Id,
                    formulaNames.GetValueOrDefault(s.FormulaId) ?? "Feed",
                    confirmed ? s.AllocatedKg : (plan?.AnimalId is null ? s.AllocatedKg : entry.ExpectedKg),
                    unit,
                    s.AllocatedCostBdt,
                    confirmed,
                    SourceOf(entry, plan),
                    plan?.AnimalId is not null);
            }).OrderBy(f => f.Feed, StringComparer.OrdinalIgnoreCase).ToList());

        var rows = herd
            .OrderBy(a => a.Tag.TagId, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var (shedId, penId) = locations.TryGetValue(a.Id, out var loc) ? loc : (null, null);
                return new FeedSheetAnimalDto(
                    a.Id,
                    a.Tag.TagId,
                    breedNames.GetValueOrDefault(a.BreedId) ?? a.Species.ToString(),
                    a.Sex.ToString(),
                    a.LatestWeightKg,
                    a.BatchId,
                    a.BatchId is { } b ? batchNames.GetValueOrDefault(b) : null,
                    shedId,
                    shedId is { } s ? shedNames.GetValueOrDefault(s) : null,
                    penId,
                    penId is { } p ? penNames.GetValueOrDefault(p) : null,
                    planNames[a.Id],
                    ownPlan.Contains(a.Id),
                    fedByAnimal.GetValueOrDefault(a.Id) ?? []);
            })
            .ToList();

        return new DailyFeedSheetDto(date, rows);
    }

    internal static string FeedName(string? formulaTitle) =>
        string.IsNullOrWhiteSpace(formulaTitle)
            ? "Feed"
            : formulaTitle.EndsWith(DirectFeedService.DirectItemSuffix, StringComparison.Ordinal)
                ? formulaTitle[..^DirectFeedService.DirectItemSuffix.Length]
                : formulaTitle;

    private static string SourceOf(DailyFeedingEntry entry, AnimalFeedingPlan? plan)
    {
        var reason = entry.AdjustmentReason ?? string.Empty;
        if (reason.StartsWith(SaveDailyFeedSheetCommandHandler.ReasonPrefix, StringComparison.Ordinal))
            return "Daily sheet";
        if (reason.StartsWith("Manual entry", StringComparison.Ordinal))
            return "Manual";
        return plan?.AnimalId is null ? "Group plan" : "Plan";
    }
}

/// <summary>A feed from the last day these animals were fed, ready to put on a new sheet.</summary>
/// <param name="Quantity">
/// In the stock item's base unit (<paramref name="Unit"/>), or kg for a recipe. Per head when
/// <paramref name="Mode"/> is PerHead; the whole group's total (for <paramref name="HeadsFed"/> animals) when GroupTotal.
/// </param>
public sealed record FeedSheetCopyLineDto(
    Guid? FormulaId,
    Guid? InventoryItemId,
    string Feed,
    decimal Quantity,
    string Unit,
    int HeadsFed,
    FeedSheetQuantityMode Mode = FeedSheetQuantityMode.PerHead);

/// <param name="Split">How any group totals were evidently split last time (by weight when amounts varied with the animal).</param>
public sealed record FeedSheetCopyDto(DateOnly? FromDate, IReadOnlyList<FeedSheetCopyLineDto> Lines, FeedSheetSplit Split = FeedSheetSplit.ByWeight);

/// <summary>
/// What the chosen animals were fed on the most recent earlier day, looking back up to 60 days, so
/// "same as last day" fills a new sheet in one tap. A feed every animal got the same amount of comes
/// back as a per-head amount. A feed whose amount varied between animals (a group total split by body
/// weight) comes back as that group total, to be split by weight again, so heavier animals still get more.
/// </summary>
public sealed record GetLastFeedSheetQuery(Guid FarmId, DateOnly Date, IReadOnlyList<Guid> AnimalIds) : IRequest<FeedSheetCopyDto>;

public sealed class GetLastFeedSheetQueryHandler(
    IAnimalFeedAllocationRepository allocations,
    IDailyFeedingEntryRepository entries,
    IFeedFormulaRepository formulas,
    IFeedIngredientRepository ingredients,
    IInventoryItemRepository inventory,
    Farm360.Application.Inventory.Units.IUnitService? units = null) : IRequestHandler<GetLastFeedSheetQuery, FeedSheetCopyDto>
{
    private const int LookBackDays = 60;

    public async Task<FeedSheetCopyDto> Handle(GetLastFeedSheetQuery request, CancellationToken cancellationToken)
    {
        var wanted = request.AnimalIds.ToHashSet();
        if (wanted.Count == 0)
            return new FeedSheetCopyDto(null, []);

        var to = request.Date.AddDays(-1);
        var shares = (await allocations.GetByFarmAsync(request.FarmId, request.Date.AddDays(-LookBackDays), to, cancellationToken: cancellationToken))
            .Where(s => wanted.Contains(s.AnimalId) && s.AllocatedKg > 0m)
            .ToList();
        if (shares.Count == 0)
            return new FeedSheetCopyDto(null, []);

        // Only feed that was really given (confirmed or adjusted).
        var status = (await entries.GetByIdsAsync(shares.Select(s => s.DailyFeedingEntryId).Distinct().ToList(), cancellationToken))
            .ToDictionary(e => e.Id, e => e.Status);
        var eaten = shares
            .Where(s => status.GetValueOrDefault(s.DailyFeedingEntryId) is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted)
            .ToList();
        if (eaten.Count == 0)
            return new FeedSheetCopyDto(null, []);

        var lastDay = eaten.Max(s => s.EntryDate);
        var lines = new List<FeedSheetCopyLineDto>();
        foreach (var feed in eaten.Where(s => s.EntryDate == lastDay).GroupBy(s => s.FormulaId))
        {
            var perAnimal = feed.GroupBy(s => s.AnimalId).Select(g => g.Sum(s => s.AllocatedKg)).ToList();
            var heads = perAnimal.Count;
            var total = perAnimal.Sum();
            var formula = await formulas.GetByIdAsync(feed.Key, cancellationToken);
            if (formula is null)
                continue;

            var mode = IsSameForEveryone(perAnimal) ? FeedSheetQuantityMode.PerHead : FeedSheetQuantityMode.GroupTotal;
            var quantity = mode == FeedSheetQuantityMode.PerHead ? total / heads : total;
            lines.Add(await ToLineAsync(formula, quantity, heads, mode, cancellationToken));
        }

        return new FeedSheetCopyDto(lastDay, lines.OrderBy(l => l.Feed, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>
    /// Equal amounts, allowing for the rounding remainder an equal split leaves on one animal
    /// (amounts are kept to 3 decimals) and 0.5% for anything else.
    /// </summary>
    public static bool IsSameForEveryone(IReadOnlyList<decimal> amounts)
    {
        if (amounts.Count <= 1)
            return true;
        var max = amounts.Max();
        var min = amounts.Min();
        return max - min <= Math.Max(0.002m * amounts.Count, max * 0.005m);
    }

    /// <summary>A stock item fed directly goes back on the sheet as that item, in its base unit; a recipe stays in kg.</summary>
    private async Task<FeedSheetCopyLineDto> ToLineAsync(FeedFormula formula, decimal perHead, int heads, FeedSheetQuantityMode mode, CancellationToken cancellationToken)
    {
        var name = GetDailyFeedSheetQueryHandler.FeedName(formula.Title);
        if (formula.Title.EndsWith(DirectFeedService.DirectItemSuffix, StringComparison.Ordinal) && formula.Ingredients.Count == 1)
        {
            var ingredient = await ingredients.GetByIdAsync(formula.Ingredients.First().IngredientId, cancellationToken);
            if (ingredient?.InventoryItemId is { } itemId && await inventory.GetByIdAsync(itemId, cancellationToken) is { } item)
            {
                // Feeding records an item in kg when its unit converts to kg, otherwise in its own unit.
                var baseQty = perHead;
                if (units is not null)
                {
                    var (_, feedUnit) = await units.ItemBaseToFeedAsync(item, 1m, cancellationToken);
                    if (string.Equals(feedUnit, "kg", StringComparison.OrdinalIgnoreCase))
                        baseQty = await units.KgToItemBaseAsync(item, perHead, cancellationToken);
                }

                return new FeedSheetCopyLineDto(null, itemId, item.Name, Math.Round(baseQty, 3), item.UnitOfMeasure, heads, mode);
            }
        }

        return new FeedSheetCopyLineDto(formula.Id, null, name, Math.Round(perHead, 3), "kg", heads, mode);
    }
}
