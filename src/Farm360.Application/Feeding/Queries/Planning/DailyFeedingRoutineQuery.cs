using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Queries.Board;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Planning;

// ── DTOs ───────────────────────────────────────────────────────────────────

/// <summary>One recipe an animal gets today, from one chart band (or extra / shed ration).</summary>
/// <param name="Band">The chart range the animal falls in ("200–250 kg", "90–180 days"), or "Fixed".</param>
/// <param name="Sessions">Feeds a day set on the chart line (Morning / Evening…).</param>
public sealed record RoutineLineDto(
    string Chart,
    string Kind,
    string Band,
    string Fit,
    Guid? FormulaId,
    string Recipe,
    decimal KgPerDay,
    int Sessions,
    decimal KgPerSession,
    decimal CostBdt);

public sealed record RoutineAnimalDto(
    Guid AnimalId,
    string Tag,
    string? Shed,
    string? Pen,
    decimal? WeightKg,
    int? AgeDays,
    IReadOnlyList<RoutineLineDto> Lines,
    decimal TotalKg,
    decimal CostBdt);

/// <summary>A feeding time and how much of the recipe to carry out then.</summary>
public sealed record RoutineSessionDto(string Session, decimal Kg, int Animals);

/// <summary>One ingredient of a recipe for today's batch, and whether stock covers it.</summary>
/// <param name="Need">In the stock item's own unit (<paramref name="Unit"/>).</param>
public sealed record RoutineIngredientDto(
    string Ingredient,
    decimal Percent,
    decimal Kg,
    string? StockItem,
    string Unit,
    decimal? Need,
    decimal? InStock,
    bool IsShort);

/// <summary>Mixing sheet for one recipe: total to mix today, split by feeding time, and its ingredients.</summary>
public sealed record RoutineRecipeDto(
    Guid FormulaId,
    string Recipe,
    bool DirectItem,
    int Animals,
    decimal TotalKg,
    decimal CostBdt,
    IReadOnlyList<RoutineSessionDto> Sessions,
    IReadOnlyList<RoutineIngredientDto> Ingredients);

public sealed record DailyFeedingRoutineDto(
    Guid FarmId,
    DateOnly Date,
    int Animals,
    int AnimalsWithoutRation,
    decimal TotalKg,
    decimal TotalCostBdt,
    IReadOnlyList<string> SessionOrder,
    IReadOnlyList<RoutineRecipeDto> Recipes,
    IReadOnlyList<RoutineAnimalDto> PerAnimal,
    IReadOnlyList<string> Notes);

// ── Query ──────────────────────────────────────────────────────────────────

/// <summary>
/// The day's feeding routine worked out from the feed charts and recipes: what each animal gets
/// (the chart band its weight or age falls in), how much of each recipe to mix, which ingredients
/// go in, and how it splits over the feeding times. Same rules as the daily job, so it matches the
/// entries the Today page will show.
/// </summary>
/// <param name="RuleSetId">Only this active feed chart (and the animals on it).</param>
/// <param name="ApplyToUnassigned">With <paramref name="RuleSetId"/>: also feed animals that have no ration yet by that chart, to see the routine before assigning it.</param>
public sealed record GetDailyFeedingRoutineQuery(
    Guid FarmId,
    DateOnly? Date = null,
    Guid? ShedId = null,
    Guid? PenId = null,
    IReadOnlyList<Guid>? AnimalIds = null,
    Guid? RuleSetId = null,
    bool ApplyToUnassigned = false) : IRequest<DailyFeedingRoutineDto>;

public sealed class GetDailyFeedingRoutineQueryHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IFeedFormulaRepository formulas,
    IFeedIngredientRepository ingredients,
    IInventoryItemRepository inventory,
    IShedRepository sheds,
    IPenRepository pens,
    IAnimalBatchRepository batches,
    IUnitService units,
    ITenantService tenant) : IRequestHandler<GetDailyFeedingRoutineQuery, DailyFeedingRoutineDto>
{
    public async Task<DailyFeedingRoutineDto> Handle(GetDailyFeedingRoutineQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenant.TenantId;
        var day = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var notes = new List<string>();

        var herd = (await animals.GetAllByFarmAsync(request.FarmId, cancellationToken))
            .Where(a => a.Status is AnimalStatus.Active or AnimalStatus.Quarantined)
            .ToList();
        var locations = await animals.GetCurrentLocationsByFarmAsync(request.FarmId, cancellationToken);
        var subjects = herd.Where(a =>
        {
            if (request.AnimalIds is { Count: > 0 } ids) return ids.Contains(a.Id);
            locations.TryGetValue(a.Id, out var p);
            if (request.PenId is { } pen && p.PenId != pen) return false;
            if (request.ShedId is { } shed && p.ShedId != shed) return false;
            return true;
        }).ToList();

        var activePlans = await plans.GetActivePlansByFarmAsync(tenantId, request.FarmId, cancellationToken);
        var sets = (await ruleSets.GetAllAsync(cancellationToken)).ToDictionary(r => r.Id);
        var formulaById = (await formulas.GetListAsync(tenantId, 1, 2000, null, cancellationToken)).ToDictionary(f => f.Id);
        var names = await LocationNames.LoadAsync(tenantId, request.FarmId, subjects, sheds, pens, batches, cancellationToken);

        FeedingRuleSet? chosen = null;
        if (request.RuleSetId is { } chosenId)
        {
            if (!sets.TryGetValue(chosenId, out chosen))
                throw new ArgumentException("That feed chart was not found.");
            if (!chosen.IsActive)
                notes.Add($"'{chosen.Name}' is inactive, so the daily entries don't use it.");
        }

        var perAnimal = new List<RoutineAnimalDto>();
        var recipeKg = new Dictionary<Guid, decimal>();
        var recipeAnimals = new Dictionary<Guid, HashSet<Guid>>();
        var recipeSessions = new Dictionary<Guid, Dictionary<string, (decimal Kg, HashSet<Guid> Animals)>>();
        var withoutRation = 0;

        foreach (var animal in subjects)
        {
            locations.TryGetValue(animal.Id, out var place);
            var weight = GetFeedRequirementQueryHandler.ProjectedWeight(animal, day, project: true);
            var age = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, day);
            var lines = new List<RoutineLineDto>();
            var hasRation = false;

            foreach (var plan in activePlans)
            {
                var own = plan.AnimalId == animal.Id;
                var group = plan.AnimalId is null && GetFeedingBoardQueryHandler.Covers(plan, animal, place.PenId, place.ShedId);
                if (!own && !group) continue;
                if (plan.StartDate > day || (plan.EndDate is { } end && end < day) || plan.IsPausedOn(day)) continue;
                if (!sets.TryGetValue(plan.FeedingRuleSetId, out var set) || !set.IsActive) continue;
                hasRation = true;
                if (chosen is not null && set.Id != chosen.Id) continue;

                // Group plans are fed per head at the group's weight, as the daily job does.
                AddLines(set, RationCalculator.KindOf(set, plan), group ? plan.TriggeredByWeightKg : weight, group ? null : age);
            }

            // Not on any ration yet: show what the chosen chart would give it.
            if (!hasRation && chosen is not null && request.ApplyToUnassigned)
                AddLines(chosen, "Not assigned", weight, age);

            void AddLines(FeedingRuleSet set, string kind, decimal? w, int? ageDays)
            {
                var match = FeedingRuleMatcher.Match(set, w, ageDays);
                foreach (var line in match.Lines)
                {
                    var kg = FeedingRuleMatcher.ExpectedKg(set, line, w ?? 0m);
                    if (kg <= 0) continue;

                    formulaById.TryGetValue(line.FormulaId, out var formula);
                    var sessions = Math.Max(1, line.SessionsPerDay);
                    lines.Add(new RoutineLineDto(
                        RationCalculator.DisplayName(set),
                        kind,
                        BandLabel(set, line),
                        match.Fit.ToString(),
                        formula?.Id,
                        formula is null ? $"{line.FeedType} (no recipe)" : RecipeName(formula),
                        Math.Round(kg, 3),
                        sessions,
                        Math.Round(kg / sessions, 3),
                        Math.Round(kg * (formula?.TotalCostPerKgBdt ?? 0m), 2)));

                    if (formula is null) continue;
                    recipeKg[formula.Id] = recipeKg.GetValueOrDefault(formula.Id) + kg;
                    if (!recipeAnimals.TryGetValue(formula.Id, out var who)) recipeAnimals[formula.Id] = who = [];
                    who.Add(animal.Id);
                    if (!recipeSessions.TryGetValue(formula.Id, out var bySession)) recipeSessions[formula.Id] = bySession = [];
                    foreach (var s in SessionNames(sessions))
                    {
                        var cur = bySession.TryGetValue(s, out var c) ? c : (0m, new HashSet<Guid>());
                        cur.Item2.Add(animal.Id);
                        bySession[s] = (cur.Item1 + kg / sessions, cur.Item2);
                    }
                }
            }

            if (lines.Count == 0)
            {
                if (!hasRation) withoutRation++;
                continue;
            }

            perAnimal.Add(new RoutineAnimalDto(
                animal.Id, animal.Tag.TagId, names.Shed(place.ShedId), names.Pen(place.PenId), weight is null ? null : Math.Round(weight.Value, 1), age,
                lines, Math.Round(lines.Sum(l => l.KgPerDay), 3), Math.Round(lines.Sum(l => l.CostBdt), 2)));
        }

        // ── Mixing sheet: each recipe's ingredients for today's total, against stock ──
        var ingredientById = (await ingredients.GetAllAsync(tenantId, includePreloaded: true, cancellationToken)).ToDictionary(i => i.Id);
        var items = (await inventory.GetByFarmIdAsync(request.FarmId, null, cancellationToken)).ToDictionary(i => i.Id);

        // Several recipes can draw on one stock item, so shortfalls are judged on the combined need.
        var itemNeed = new Dictionary<Guid, decimal>();
        var draft = new List<(Guid FormulaId, List<(string Name, decimal Pct, decimal Kg, InventoryItem? Item, decimal? Need)> Rows)>();
        foreach (var (formulaId, kg) in recipeKg)
        {
            var formula = formulaById[formulaId];
            var totalPct = formula.Ingredients.Sum(i => i.Percentage);
            if (totalPct <= 0) totalPct = 100m;
            var rows = new List<(string, decimal, decimal, InventoryItem?, decimal?)>();
            foreach (var fi in formula.Ingredients.OrderByDescending(i => i.Percentage))
            {
                var share = kg * fi.Percentage / totalPct;
                ingredientById.TryGetValue(fi.IngredientId, out var ingredient);
                InventoryItem? item = null;
                decimal? need = null;
                if (ingredient?.InventoryItemId is { } itemId && items.TryGetValue(itemId, out var found))
                {
                    item = found;
                    need = await units.KgToItemBaseAsync(found, share, cancellationToken);
                    itemNeed[itemId] = itemNeed.GetValueOrDefault(itemId) + need.Value;
                }
                rows.Add((ingredient?.Name ?? "Unknown ingredient", Math.Round(fi.Percentage * 100m / totalPct, 2), share, item, need));
            }
            draft.Add((formulaId, rows));
        }

        var sessionOrder = recipeSessions.Values.SelectMany(s => s.Keys).Distinct().OrderBy(SessionRank).ToList();
        var recipes = draft.Select(d =>
        {
            var formula = formulaById[d.FormulaId];
            var kg = recipeKg[d.FormulaId];
            return new RoutineRecipeDto(
                formula.Id,
                RecipeName(formula),
                formula.Title.EndsWith(DirectFeedService.DirectItemSuffix, StringComparison.Ordinal),
                recipeAnimals[formula.Id].Count,
                Math.Round(kg, 3),
                Math.Round(kg * formula.TotalCostPerKgBdt, 2),
                recipeSessions[formula.Id].OrderBy(s => SessionRank(s.Key))
                    .Select(s => new RoutineSessionDto(s.Key, Math.Round(s.Value.Kg, 3), s.Value.Animals.Count)).ToList(),
                d.Rows.Select(r => new RoutineIngredientDto(
                    r.Name, r.Pct, Math.Round(r.Kg, 3), r.Item?.Name, r.Item?.UnitOfMeasure ?? "kg",
                    r.Need is null ? null : Math.Round(r.Need.Value, 3),
                    r.Item is null ? null : Math.Round(r.Item.CurrentStock, 3),
                    r.Item is not null && itemNeed[r.Item.Id] > r.Item.CurrentStock)).ToList());
        })
        .OrderByDescending(r => r.TotalKg)
        .ToList();

        if (withoutRation > 0 && !(chosen is not null && request.ApplyToUnassigned))
            notes.Add($"{withoutRation} animal(s) have no ration for this day — set one from Who Eats What.");
        var unassigned = perAnimal.Count(a => a.Lines.Any(l => l.Kind == "Not assigned"));
        if (unassigned > 0)
            notes.Add($"{unassigned} animal(s) are not on '{chosen!.Name}' yet — shown as if they were. Assign it from Who Eats What to feed them by it.");
        if (recipes.Any(r => r.Ingredients.Any(i => i.IsShort)))
            notes.Add("Some ingredients are short in stock for today's feeding — see the mixing sheet.");
        if (perAnimal.Any(a => a.Lines.Any(l => l.Fit is nameof(RuleMatchFit.BelowRange) or nameof(RuleMatchFit.AboveRange))))
            notes.Add("Some animals are outside their chart's ranges and get the nearest band — check their weight or chart.");
        if (perAnimal.Any(a => a.WeightKg is null && a.Lines.Any(l => l.Kind == "Chart")))
            notes.Add("Some animals have no weight recorded, so weight-based charts use their first band.");

        var ordered = perAnimal
            .OrderBy(a => a.Shed ?? "~", StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Pen ?? "~", StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.Tag, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DailyFeedingRoutineDto(
            request.FarmId, day, ordered.Count, withoutRation,
            Math.Round(ordered.Sum(a => a.TotalKg), 3), Math.Round(ordered.Sum(a => a.CostBdt), 2),
            sessionOrder, recipes, ordered, notes);
    }

    private static string RecipeName(FeedFormula formula) =>
        formula.Title.Replace(DirectFeedService.DirectItemSuffix, string.Empty, StringComparison.Ordinal);

    /// <summary>The chart range a line covers, as the user typed it.</summary>
    internal static string BandLabel(FeedingRuleSet set, FeedingRuleLine line)
    {
        static string N(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        if (set.PlanType == FeedingPlanType.FixedQuantity) return "Fixed";
        if (set.PlanType == FeedingPlanType.AgeBased || line.MinAgeDays is not null || line.MaxAgeDays is not null)
        {
            if (line.MinAgeDays is null && line.MaxAgeDays is null) return "All ages";
            return line.MaxAgeDays is null ? $"{line.MinAgeDays}+ days"
                : $"{line.MinAgeDays ?? 0}–{line.MaxAgeDays} days";
        }
        if (line.MinWeightKg is null && line.MaxWeightKg is null) return "Fixed";
        return line.MaxWeightKg is null ? $"{N(line.MinWeightKg!.Value)}+ kg"
            : $"{N(line.MinWeightKg ?? 0m)}–{N(line.MaxWeightKg.Value)} kg";
    }

    /// <summary>Names for a line's feeds a day: 2 → Morning, Evening; 3 → Morning, Noon, Evening.</summary>
    internal static IReadOnlyList<string> SessionNames(int sessions) => sessions switch
    {
        <= 1 => ["Once a day"],
        2 => ["Morning", "Evening"],
        3 => ["Morning", "Noon", "Evening"],
        4 => ["Morning", "Noon", "Afternoon", "Evening"],
        _ => Enumerable.Range(1, sessions).Select(i => $"Feed {i}").ToList(),
    };

    private static int SessionRank(string s) => s switch
    {
        "Once a day" => 0,
        "Morning" => 1,
        "Noon" => 2,
        "Afternoon" => 3,
        "Evening" => 4,
        _ => 10 + (int.TryParse(s.AsSpan(5), out var n) ? n : 0),
    };
}
