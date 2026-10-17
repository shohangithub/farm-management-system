using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Queries.Board;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Planning;

// ── DTOs ───────────────────────────────────────────────────────────────────

/// <summary>One stock item the selected animals need for the period, and what to buy.</summary>
/// <param name="BaseUnit">The unit stock is counted in (kg, g, ml…).</param>
/// <param name="ToBuy">Need − in stock, never below zero.</param>
/// <param name="PackName">The item's usual purchase unit ("Bag (50 kg)"), when set.</param>
/// <param name="PacksToBuy">Whole packs covering <paramref name="ToBuy"/>.</param>
public sealed record ItemNeedDto(
    Guid? InventoryItemId,
    string Name,
    string Category,
    string BaseUnit,
    decimal PerDay,
    decimal Total,
    decimal InStock,
    decimal ToBuy,
    string? PackName,
    decimal? PackSize,
    int? PacksToBuy,
    decimal UnitCostBdt,
    decimal CostOfNeedBdt,
    decimal CostToBuyBdt,
    IReadOnlyList<string> UsedIn,
    bool LinkedToStock);

/// <summary>A recipe (formula) to mix for the period, in kg.</summary>
public sealed record RecipeNeedDto(Guid FormulaId, string Recipe, decimal PerDayKg, decimal TotalKg, decimal CostBdt);

public sealed record AnimalNeedDto(Guid AnimalId, string Tag, string? Shed, decimal? WeightKg, decimal PerDayKg, decimal TotalKg, decimal CostBdt);

public sealed record FeedRequirementDto(
    Guid FarmId,
    DateOnly FromDate,
    DateOnly ToDate,
    int Days,
    int Animals,
    int AnimalsWithoutRation,
    bool ProjectedGrowth,
    IReadOnlyList<ItemNeedDto> Items,
    IReadOnlyList<RecipeNeedDto> Recipes,
    IReadOnlyList<AnimalNeedDto> PerAnimal,
    decimal TotalCostOfNeedBdt,
    decimal TotalCostToBuyBdt,
    IReadOnlyList<string> Notes);

// ── Query ──────────────────────────────────────────────────────────────────

/// <summary>
/// "What do I need to buy for the next N days?" — every ration feeding the selected animals
/// (feed charts, extras, shed/batch rations), day by day, broken down into recipe ingredients and
/// stock items, against what is in stock, rounded up to the packs they are bought in.
/// </summary>
/// <param name="AnimalIds">Specific animals; otherwise the shed / pen / batch, otherwise every animal.</param>
/// <param name="ProjectGrowth">Grow each animal's weight by its average daily gain, so weight-based charts step up during the period.</param>
/// <param name="IncludeConsumables">Add the farm's daily consumable plans (whole farm only).</param>
public sealed record GetFeedRequirementQuery(
    Guid FarmId,
    int Days,
    DateOnly? FromDate = null,
    IReadOnlyList<Guid>? AnimalIds = null,
    Guid? ShedId = null,
    Guid? PenId = null,
    Guid? BatchId = null,
    bool ProjectGrowth = true,
    bool IncludeConsumables = true) : IRequest<FeedRequirementDto>;

public sealed class GetFeedRequirementQueryHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IFeedFormulaRepository formulas,
    IFeedIngredientRepository ingredients,
    IInventoryItemRepository inventory,
    IConsumableUsagePlanRepository consumables,
    IShedRepository sheds,
    IPenRepository pens,
    IAnimalBatchRepository batches,
    IUnitService units,
    ITenantService tenant) : IRequestHandler<GetFeedRequirementQuery, FeedRequirementDto>
{
    public async Task<FeedRequirementDto> Handle(GetFeedRequirementQuery request, CancellationToken cancellationToken)
    {
        if (request.Days is < 1 or > 366)
            throw new ArgumentException("Choose between 1 and 366 days.");

        var tenantId = tenant.TenantId;
        var from = request.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(request.Days - 1);
        var notes = new List<string>();

        // ── Who ──
        var herd = (await animals.GetAllByFarmAsync(request.FarmId, cancellationToken))
            .Where(a => a.Status is AnimalStatus.Active or AnimalStatus.Quarantined)
            .ToList();
        var locations = await animals.GetCurrentLocationsByFarmAsync(request.FarmId, cancellationToken);
        var subjects = SelectAnimals(request, herd, locations);
        var wholeFarm = (request.AnimalIds?.Count ?? 0) == 0 && request.ShedId is null && request.PenId is null && request.BatchId is null;

        // ── Look-ups, loaded once ──
        var activePlans = await plans.GetActivePlansByFarmAsync(tenantId, request.FarmId, cancellationToken);
        var sets = (await ruleSets.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id);
        var formulaById = (await formulas.GetListAsync(tenantId, 1, 2000, null, cancellationToken)).ToDictionary(f => f.Id);
        var ingredientById = (await ingredients.GetAllAsync(tenantId, includePreloaded: true, cancellationToken)).ToDictionary(i => i.Id);
        var items = (await inventory.GetByFarmIdAsync(request.FarmId, null, cancellationToken)).ToDictionary(i => i.Id);
        var allUnits = await units.GetUnitsAsync(cancellationToken);

        // Accumulators: stock item (or unlinked ingredient) → kg of feed; recipe → kg; animal → kg & cost.
        var itemKg = new Dictionary<Guid, decimal>();
        var itemUsedIn = new Dictionary<Guid, HashSet<string>>();
        var unlinkedKg = new Dictionary<Guid, (string Name, decimal Kg, decimal CostPerKg, HashSet<string> UsedIn)>();
        var recipeKg = new Dictionary<Guid, decimal>();
        var perAnimal = new List<AnimalNeedDto>();
        var withoutRation = 0;

        foreach (var animal in subjects)
        {
            locations.TryGetValue(animal.Id, out var place);
            var ownPlans = activePlans.Where(p => p.AnimalId == animal.Id).ToList();
            var groupPlans = activePlans.Where(p => p.AnimalId is null && GetFeedingBoardQueryHandler.Covers(p, animal, place.PenId, place.ShedId)).ToList();
            if (ownPlans.Count == 0 && groupPlans.Count == 0)
            {
                withoutRation++;
                continue;
            }

            decimal animalKg = 0m, animalCost = 0m;
            var baseAge = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, from);
            for (var d = 0; d < request.Days; d++)
            {
                var day = from.AddDays(d);
                var weight = ProjectedWeight(animal, day, request.ProjectGrowth);
                var age = baseAge + d;

                foreach (var plan in ownPlans.Concat(groupPlans))
                {
                    if (plan.StartDate > day || (plan.EndDate is { } end && end < day) || plan.IsPausedOn(day))
                        continue;
                    if (!sets.TryGetValue(plan.FeedingRuleSetId, out var set) || !set.IsActive)
                        continue;

                    // Group plans are fed per head at the group's weight, as the daily job does.
                    var isGroup = plan.AnimalId is null;
                    var match = FeedingRuleMatcher.Match(set, isGroup ? plan.TriggeredByWeightKg : weight, isGroup ? null : age);
                    foreach (var line in match.Lines)
                    {
                        var kg = FeedingRuleMatcher.ExpectedKg(set, line, isGroup ? plan.TriggeredByWeightKg ?? 0m : weight ?? 0m);
                        if (kg <= 0) continue;
                        animalKg += kg;

                        if (!formulaById.TryGetValue(line.FormulaId, out var formula))
                        {
                            AddUnlinked(unlinkedKg, Farm360.Application.Finance.Services.LedgerPosting.SubKey(Guid.Empty, line.FeedType.ToString()),
                                $"{line.FeedType} (no recipe)", kg, 0m, set.Name);
                            continue;
                        }

                        animalCost += kg * formula.TotalCostPerKgBdt;
                        recipeKg[formula.Id] = recipeKg.GetValueOrDefault(formula.Id) + kg;
                        Explode(formula, kg, ingredientById, items, itemKg, itemUsedIn, unlinkedKg);
                    }
                }
            }

            perAnimal.Add(new AnimalNeedDto(animal.Id, animal.Tag.TagId, null, animal.LatestWeightKg,
                Math.Round(animalKg / request.Days, 3), Math.Round(animalKg, 3), Math.Round(animalCost, 2)));
        }

        // ── Turn feed kg into each stock item's own unit, and compare with stock ──
        var itemNeeds = new List<ItemNeedDto>();
        var directNeedBase = new Dictionary<Guid, decimal>();
        foreach (var (itemId, kg) in itemKg)
        {
            var item = items[itemId];
            directNeedBase[itemId] = await units.KgToItemBaseAsync(item, kg, cancellationToken);
        }

        // Daily consumables are farm-wide, so they belong only to a whole-farm plan.
        if (request.IncludeConsumables && wholeFarm)
        {
            foreach (var cp in (await consumables.GetByFarmIdAsync(request.FarmId, includeItems: true, cancellationToken))
                         .Where(p => p.Status == ConsumableUsagePlanStatus.Active))
            {
                var activeDays = Enumerable.Range(0, request.Days).Select(from.AddDays)
                    .Count(day => cp.StartDate <= day && (cp.EndDate is null || cp.EndDate >= day));
                foreach (var ci in cp.Items.Where(i => items.ContainsKey(i.InventoryItemId)))
                {
                    directNeedBase[ci.InventoryItemId] = directNeedBase.GetValueOrDefault(ci.InventoryItemId) + ci.PlannedQuantityPerDay * activeDays;
                    if (!itemUsedIn.TryGetValue(ci.InventoryItemId, out var set)) itemUsedIn[ci.InventoryItemId] = set = [];
                    set.Add($"Consumables: {cp.Name}");
                }
            }
        }

        foreach (var (itemId, total) in directNeedBase)
        {
            var item = items[itemId];
            var pack = item.DefaultUnitId is { } du
                ? allUnits.SelectMany(u => u.Conversions).FirstOrDefault(c => c.Id == du && c.Factor > 1m)
                : null;
            var toBuy = Math.Max(0m, total - item.CurrentStock);
            itemNeeds.Add(new ItemNeedDto(
                item.Id, item.Name, item.Category.ToString(), item.UnitOfMeasure,
                Math.Round(total / request.Days, 3), Math.Round(total, 3), Math.Round(item.CurrentStock, 3), Math.Round(toBuy, 3),
                pack?.Name, pack?.Factor, pack is null ? null : (int)Math.Ceiling(toBuy / pack.Factor),
                Math.Round(item.WeightedAverageCostBdt, 4),
                Math.Round(total * item.WeightedAverageCostBdt, 2),
                Math.Round(toBuy * item.WeightedAverageCostBdt, 2),
                (itemUsedIn.GetValueOrDefault(itemId) ?? []).OrderBy(x => x).ToList(),
                true));
        }

        foreach (var (key, u) in unlinkedKg)
        {
            itemNeeds.Add(new ItemNeedDto(null, u.Name, "Not linked to stock", "kg",
                Math.Round(u.Kg / request.Days, 3), Math.Round(u.Kg, 3), 0m, Math.Round(u.Kg, 3), null, null, null,
                Math.Round(u.CostPerKg, 4), Math.Round(u.Kg * u.CostPerKg, 2), Math.Round(u.Kg * u.CostPerKg, 2),
                u.UsedIn.OrderBy(x => x).ToList(), false));
        }

        if (unlinkedKg.Count > 0)
            notes.Add("Some recipe ingredients are not linked to a stock item, so stock on hand can't be checked for them. Link them in Feeding → Recipes → Ingredients.");
        if (withoutRation > 0)
            notes.Add($"{withoutRation} selected animal(s) have no ration and are not included.");
        if (request.ProjectGrowth)
            notes.Add("Weights are grown by each animal's average daily gain, so weight-based charts step up during the period.");

        var recipeNeeds = recipeKg
            .Select(r => new RecipeNeedDto(r.Key, formulaById[r.Key].Title, Math.Round(r.Value / request.Days, 3), Math.Round(r.Value, 3),
                Math.Round(r.Value * formulaById[r.Key].TotalCostPerKgBdt, 2)))
            .OrderByDescending(r => r.TotalKg)
            .ToList();

        var names = await LocationNames.LoadAsync(tenantId, request.FarmId, subjects, sheds, pens, batches, cancellationToken);
        perAnimal = perAnimal
            .Select(a => a with { Shed = names.Shed(locations.GetValueOrDefault(a.AnimalId).ShedId) })
            .OrderBy(a => a.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var orderedItems = itemNeeds
            .OrderByDescending(i => i.ToBuy > 0)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FeedRequirementDto(
            request.FarmId, from, to, request.Days,
            perAnimal.Count, withoutRation, request.ProjectGrowth,
            orderedItems, recipeNeeds, perAnimal,
            Math.Round(orderedItems.Sum(i => i.CostOfNeedBdt), 2),
            Math.Round(orderedItems.Sum(i => i.CostToBuyBdt), 2),
            notes);
    }

    private static List<Animal> SelectAnimals(GetFeedRequirementQuery request, List<Animal> herd,
        IReadOnlyDictionary<Guid, (Guid? ShedId, Guid? PenId)> locations)
    {
        if (request.AnimalIds is { Count: > 0 } ids)
            return herd.Where(a => ids.Contains(a.Id)).ToList();

        return herd.Where(a =>
        {
            locations.TryGetValue(a.Id, out var p);
            if (request.PenId is { } pen && p.PenId != pen) return false;
            if (request.ShedId is { } shed && p.ShedId != shed) return false;
            if (request.BatchId is { } batch && a.BatchId != batch) return false;
            return true;
        }).ToList();
    }

    /// <summary>Latest weight, grown by the animal's average daily gain up to <paramref name="day"/>.</summary>
    internal static decimal? ProjectedWeight(Animal animal, DateOnly day, bool project)
    {
        if (animal.LatestWeightKg is not { } w || w <= 0)
            return null;
        if (!project || animal.AdgKgPerDay is not { } adg || adg <= 0 || animal.LatestWeightDate is not { } on)
            return w;
        var days = day.DayNumber - on.DayNumber;
        return days > 0 ? w + adg * days : w;
    }

    /// <summary>Splits kg of a recipe into its ingredients and, through them, its stock items.</summary>
    private static void Explode(
        FeedFormula formula, decimal kg,
        Dictionary<Guid, FeedIngredient> ingredientById,
        Dictionary<Guid, InventoryItem> items,
        Dictionary<Guid, decimal> itemKg,
        Dictionary<Guid, HashSet<string>> itemUsedIn,
        Dictionary<Guid, (string Name, decimal Kg, decimal CostPerKg, HashSet<string> UsedIn)> unlinked)
    {
        var totalPct = formula.Ingredients.Sum(i => i.Percentage);
        if (totalPct <= 0) totalPct = 100m;

        foreach (var fi in formula.Ingredients)
        {
            var share = kg * fi.Percentage / totalPct;
            if (share <= 0) continue;

            ingredientById.TryGetValue(fi.IngredientId, out var ingredient);
            if (ingredient?.InventoryItemId is { } itemId && items.ContainsKey(itemId))
            {
                itemKg[itemId] = itemKg.GetValueOrDefault(itemId) + share;
                if (!itemUsedIn.TryGetValue(itemId, out var used)) itemUsedIn[itemId] = used = [];
                used.Add(formula.Title.Replace(DirectFeedService.DirectItemSuffix, " (fed directly)", StringComparison.Ordinal));
            }
            else
            {
                AddUnlinked(unlinked, fi.IngredientId, ingredient?.Name ?? "Unknown ingredient", share, fi.IngredientCostPerKg, formula.Title);
            }
        }
    }

    private static void AddUnlinked(
        Dictionary<Guid, (string Name, decimal Kg, decimal CostPerKg, HashSet<string> UsedIn)> unlinked,
        Guid key, string name, decimal kg, decimal costPerKg, string usedIn)
    {
        if (!unlinked.TryGetValue(key, out var u))
            u = (name, 0m, costPerKg, []);
        u.UsedIn.Add(usedIn);
        unlinked[key] = (u.Name, u.Kg + kg, u.CostPerKg, u.UsedIn);
    }
}
