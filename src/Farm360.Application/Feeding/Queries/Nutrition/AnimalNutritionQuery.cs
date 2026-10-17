using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Nutrition;

// ── DTOs ───────────────────────────────────────────────────────────────────

/// <summary>One day: what the animal ate, the nutrients in it, and what an animal of its weight needs.</summary>
/// <param name="WeightKg">Weighed on the day, or interpolated between weighings (see <paramref name="WeightIsMeasured"/>).</param>
public sealed record NutritionDayDto(
    DateOnly Date,
    decimal? WeightKg,
    bool WeightIsMeasured,
    decimal FeedKg,
    decimal DryMatterKg,
    decimal ProteinG,
    decimal EnergyMj,
    decimal FibreG,
    decimal CalciumG,
    decimal PhosphorusG,
    decimal? DryMatterNeedKg,
    decimal? ProteinNeedG,
    decimal? EnergyNeedMj,
    decimal? ProteinPercentOfNeed,
    decimal? EnergyPercentOfNeed,
    string Recipes);

public sealed record NutritionSummaryDto(
    int Days,
    int FedDays,
    decimal? StartWeightKg,
    decimal? EndWeightKg,
    decimal? GainKg,
    decimal? AdgKgPerDay,
    decimal TotalFeedKg,
    decimal TotalDryMatterKg,
    decimal AvgFeedKgPerDay,
    decimal AvgDryMatterKgPerDay,
    decimal AvgProteinGPerDay,
    decimal AvgEnergyMjPerDay,
    decimal DietProteinPercent,
    decimal DietEnergyMjPerKgDm,
    decimal? DryMatterPercentOfBodyWeight,
    decimal? AvgProteinPercentOfNeed,
    decimal? AvgEnergyPercentOfNeed,
    decimal? FeedConversionKgDmPerKgGain,
    decimal? ExpectedAdgFromEnergy,
    string Verdict,
    IReadOnlyList<string> Advice);

public sealed record AnimalNutritionDto(
    Guid AnimalId,
    string Tag,
    string Species,
    DateOnly From,
    DateOnly To,
    NutritionSummaryDto Summary,
    IReadOnlyList<NutritionDayDto> Days,
    IReadOnlyList<string> Notes);

// ── Query ──────────────────────────────────────────────────────────────────

/// <summary>
/// What a cow actually ate — dry matter, protein, energy, fibre, calcium and phosphorus — day by day
/// against what an animal of its weight and growth needs, with its weight gain, ADG and feed
/// conversion, so the ration can be judged by results.
/// </summary>
/// <remarks>
/// <para>
/// Intake comes from the animal's feed allocations (its own ration and its share of group rations),
/// each recipe split into its ingredients using the nutrition stored on the recipe. Ingredient
/// protein, energy, fibre and minerals are read as values per kg of <b>dry matter</b>, the way feed
/// tables give them; dry matter % converts the fed (as-fed) kg.
/// </para>
/// <para>
/// Needs are field guides for growing/fattening cattle, not a lab ration formulation:
/// dry matter intake ≈ 2.5% of body weight; energy (ME) = maintenance (AFRC, about 35 MJ at 300 kg) +
/// about 35 MJ per kg of daily gain; protein = 14% of the dry matter needed under 250 kg, 12% to
/// 400 kg and 11% above.
/// </para>
/// </remarks>
public sealed record GetAnimalNutritionQuery(Guid AnimalId, DateOnly From, DateOnly To) : IRequest<AnimalNutritionDto>;

public sealed class GetAnimalNutritionQueryHandler(
    IAnimalRepository animals,
    IAnimalFeedAllocationRepository allocations,
    IFeedFormulaRepository formulas) : IRequestHandler<GetAnimalNutritionQuery, AnimalNutritionDto>
{
    public const decimal DmIntakeShare = 0.025m;
    public const decimal EnergyPerKgGainMj = 35m;

    public async Task<AnimalNutritionDto> Handle(GetAnimalNutritionQuery request, CancellationToken cancellationToken)
    {
        if (request.To < request.From)
            throw new ArgumentException("The period end must be on or after its start.");

        var animal = await animals.GetByIdAsync(request.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"Animal '{request.AnimalId}' was not found.");

        var notes = new List<string>();
        var weights = animal.WeightRecords
            .Select(w => (w.RecordedDate, Kg: w.Weight.WeightKg))
            .GroupBy(w => w.RecordedDate)
            .Select(g => (Date: g.Key, Kg: g.Last().Kg))
            .OrderBy(w => w.Date)
            .ToList();

        var fed = await allocations.GetByAnimalAsync(request.AnimalId, request.From, request.To, cancellationToken);
        var recipeCache = new Dictionary<Guid, FeedFormula?>();
        foreach (var id in fed.Select(a => a.FormulaId).Distinct())
            recipeCache[id] = await formulas.GetByIdAsync(id, cancellationToken);

        if (recipeCache.Values.Any(f => f is not null && f.Ingredients.All(i => i.IngredientNutritionalProfile.DryMatterPercentage == 0)))
            notes.Add("Some recipes have no nutrition values on their ingredients, so their protein and energy count as zero. Fill them in under Feeding → Recipes → Ingredients.");

        // Growth over the period, from weighings (needed for the energy-for-gain part of the need).
        var startW = WeightOn(weights, request.From, out _);
        var endW = WeightOn(weights, request.To, out _);
        var days = request.To.DayNumber - request.From.DayNumber + 1;
        decimal? adg = startW is { } s && endW is { } e && days > 1 ? (e - s) / (days - 1) : null;
        var adgForNeeds = adg is > 0 ? adg.Value : animal.AdgKgPerDay is > 0 ? animal.AdgKgPerDay.Value : 0m;
        if (adg is null && weights.Count < 2)
            notes.Add("Fewer than two weighings — weight and growth are estimated. Weigh the animal at the start and end of the period for a true picture.");

        var byDay = fed.GroupBy(a => a.EntryDate).ToDictionary(g => g.Key, g => g.ToList());
        var rows = new List<NutritionDayDto>(days);
        for (var day = request.From; day <= request.To; day = day.AddDays(1))
        {
            var weight = WeightOn(weights, day, out var measured);
            decimal feedKg = 0, dm = 0, cp = 0, me = 0, cf = 0, ca = 0, p = 0;
            var recipes = new HashSet<string>();
            foreach (var a in byDay.GetValueOrDefault(day) ?? [])
            {
                feedKg += a.AllocatedKg;
                if (recipeCache.GetValueOrDefault(a.FormulaId) is not { } recipe)
                    continue;
                recipes.Add(recipe.Title);
                var totalPct = recipe.Ingredients.Sum(i => i.Percentage);
                if (totalPct <= 0) continue;
                foreach (var ing in recipe.Ingredients)
                {
                    var n = ing.IngredientNutritionalProfile;
                    var asFed = a.AllocatedKg * ing.Percentage / totalPct;
                    var dmKg = asFed * n.DryMatterPercentage / 100m;
                    dm += dmKg;
                    cp += dmKg * n.CrudeProteinPercentage * 10m;   // g
                    me += dmKg * n.MetabolizableEnergyMjPerKg;     // MJ
                    cf += dmKg * n.CrudeFiberPercentage * 10m;     // g
                    ca += dmKg * n.CalciumPercentage * 10m;        // g
                    p += dmKg * n.PhosphorusPercentage * 10m;      // g
                }
            }

            var need = Needs(weight, adgForNeeds);
            rows.Add(new NutritionDayDto(
                day, R(weight, 1), measured, R(feedKg, 2), R(dm, 2), R(cp, 0), R(me, 1), R(cf, 0), R(ca, 1), R(p, 1),
                R(need?.DmKg, 2), R(need?.CpG, 0), R(need?.MeMj, 1),
                need is { CpG: > 0 } && feedKg > 0 ? R(cp / need.Value.CpG * 100m, 0) : null,
                need is { MeMj: > 0 } && feedKg > 0 ? R(me / need.Value.MeMj * 100m, 0) : null,
                string.Join(", ", recipes.OrderBy(x => x))));
        }

        return new AnimalNutritionDto(animal.Id, animal.Tag.TagId, animal.Species.ToString(), request.From, request.To,
            Summarise(rows, startW, endW, adg, days), rows, notes);
    }

    private static NutritionSummaryDto Summarise(List<NutritionDayDto> rows, decimal? startW, decimal? endW, decimal? adg, int days)
    {
        var fedRows = rows.Where(r => r.FeedKg > 0).ToList();
        var fedDays = fedRows.Count;
        decimal Avg(Func<NutritionDayDto, decimal> f) => fedDays == 0 ? 0 : fedRows.Sum(f) / fedDays;

        var totalDm = rows.Sum(r => r.DryMatterKg);
        var totalCp = rows.Sum(r => r.ProteinG);
        var totalMe = rows.Sum(r => r.EnergyMj);
        var dietCp = totalDm > 0 ? totalCp / 10m / totalDm : 0m;          // % of DM
        var dietMe = totalDm > 0 ? totalMe / totalDm : 0m;                // MJ/kg DM
        var avgWeight = fedRows.Where(r => r.WeightKg.HasValue).Select(r => r.WeightKg!.Value).DefaultIfEmpty().Average();
        decimal? dmPctBw = avgWeight > 0 ? Avg(r => r.DryMatterKg) / avgWeight * 100m : null;
        var cpPct = fedRows.Where(r => r.ProteinPercentOfNeed.HasValue).Select(r => r.ProteinPercentOfNeed!.Value).DefaultIfEmpty().Average();
        var mePct = fedRows.Where(r => r.EnergyPercentOfNeed.HasValue).Select(r => r.EnergyPercentOfNeed!.Value).DefaultIfEmpty().Average();
        decimal? gain = startW is { } s && endW is { } e ? e - s : null;
        decimal? fcr = gain is > 0 ? totalDm / gain.Value : null;

        // Gain the energy eaten above maintenance could support (35 MJ per kg of gain).
        decimal? expectedAdg = null;
        if (avgWeight > 0 && fedDays > 0)
        {
            expectedAdg = Math.Max(0m, (Avg(r => r.EnergyMj) - MaintenanceMj(avgWeight)) / EnergyPerKgGainMj);
        }

        var advice = new List<string>();
        if (fedDays == 0) advice.Add("No feeding recorded in this period.");
        if (fedDays > 0 && fedDays < days) advice.Add($"Fed on {fedDays} of {days} days — check for missed or unconfirmed feeding.");
        if (mePct is > 0 and < 90) advice.Add("Energy is below need: add energy (grain, maize, molasses) or more feed — growth will be slower than possible.");
        if (mePct > 125) advice.Add("Energy is well above need: check for over-feeding and cost per kg gain.");
        if (cpPct is > 0 and < 90) advice.Add("Protein is below need: add a protein source (khesari/soya/mustard cake, DCP) — frame and muscle growth suffer.");
        if (cpPct > 140) advice.Add("Protein is well above need: it costs money and is wasted as urea.");
        if (dietMe is > 0 and < 9.5m) advice.Add($"Energy density {dietMe:0.0} MJ/kg DM is low for fattening (aim ~10.5–11.5): more concentrate, less straw.");
        if (dmPctBw is > 0 and < 2m) advice.Add($"Dry matter intake is only {dmPctBw:0.0}% of body weight (aim 2.2–3%).");
        if (dmPctBw > 3.5m) advice.Add($"Dry matter intake {dmPctBw:0.0}% of body weight is unusually high — check the weight record or feed amounts.");
        if (adg is { } a && expectedAdg is { } x && x > 0.3m && a < x * 0.6m)
            advice.Add($"Actual gain {a:0.00} kg/day is well below the {x:0.00} kg/day the energy eaten should give — check health (worms, illness), feed waste, or the weighings.");
        if (fcr > 10m) advice.Add($"Feed conversion {fcr:0.0} kg DM per kg gain is poor (good fattening: 6–8).");

        var verdict = fedDays == 0 ? "No data"
            : (mePct is >= 90 and <= 125) && (cpPct is >= 90 and <= 140) ? "Well balanced"
            : mePct is > 0 and < 90 || cpPct is > 0 and < 90 ? "Under-fed"
            : "Over-fed";
        if (advice.Count == 0) advice.Add("Ration meets this animal's protein and energy needs.");

        return new NutritionSummaryDto(
            days, fedDays, R(startW, 1), R(endW, 1), R(gain, 1), R(adg, 3),
            R(rows.Sum(r => r.FeedKg), 1), R(totalDm, 1),
            R(Avg(r => r.FeedKg), 2), R(Avg(r => r.DryMatterKg), 2),
            R(Avg(r => r.ProteinG), 0), R(Avg(r => r.EnergyMj), 1),
            R(dietCp, 1), R(dietMe, 2), R(dmPctBw, 2),
            cpPct > 0 ? R(cpPct, 0) : null, mePct > 0 ? R(mePct, 0) : null,
            R(fcr, 1), R(expectedAdg, 3), verdict, advice);
    }

    /// <summary>Daily dry matter (kg), protein (g) and energy (MJ ME) an animal of this weight and gain needs.</summary>
    public static (decimal DmKg, decimal CpG, decimal MeMj)? Needs(decimal? weightKg, decimal adg)
    {
        if (weightKg is not { } w || w <= 0)
            return null;
        var dm = w * DmIntakeShare;
        var me = MaintenanceMj(w) + Math.Max(0m, adg) * EnergyPerKgGainMj;
        var cpShare = w < 250m ? 0.14m : w < 400m ? 0.12m : 0.11m;
        return (dm, dm * cpShare * 1000m, me);
    }

    /// <summary>
    /// Metabolisable energy for maintenance (MJ/day), AFRC: fasting metabolism 0.53 × (W/1.08)^0.67
    /// plus activity 0.0071 × W, at a maintenance efficiency of 0.72 (≈ 35 MJ for a 300 kg animal).
    /// </summary>
    public static decimal MaintenanceMj(decimal weightKg) =>
        ((decimal)(0.53 * Math.Pow((double)(weightKg / 1.08m), 0.67)) + 0.0071m * weightKg) / 0.72m;

    /// <summary>Weight on a day: the weighing that day, else straight-line between the nearest weighings, else the nearest one.</summary>
    internal static decimal? WeightOn(List<(DateOnly Date, decimal Kg)> weights, DateOnly day, out bool measured)
    {
        measured = false;
        if (weights.Count == 0) return null;
        var exact = weights.FindIndex(w => w.Date == day);
        if (exact >= 0) { measured = true; return weights[exact].Kg; }

        var before = weights.LastOrDefault(w => w.Date < day);
        var after = weights.FirstOrDefault(w => w.Date > day);
        if (before.Date == default) return after.Kg;
        if (after.Date == default) return before.Kg;
        var span = after.Date.DayNumber - before.Date.DayNumber;
        return before.Kg + (after.Kg - before.Kg) * (day.DayNumber - before.Date.DayNumber) / span;
    }

    private static decimal? R(decimal? v, int d) => v is { } x ? Math.Round(x, d) : null;

    private static decimal R(decimal v, int d) => Math.Round(v, d);
}
