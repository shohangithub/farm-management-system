using Farm360.Application.Common.Interfaces;
using Farm360.Application.Intelligence.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>
/// "Should I sell this animal now, or keep it, and for how long?" — the one service behind the
/// Sell or Keep report, the animal page card and the livestock list badges.
/// </summary>
public interface IAnimalSellOrKeepAdvisor
{
    /// <summary>The assumptions in use.</summary>
    SellOrKeepAssumptions Assumptions { get; }

    /// <summary>Advice for one animal on the farm, with its day-by-day projection. Null when not found or no longer on the farm.</summary>
    Task<SellOrKeepAdvice?> AdviseAnimalAsync(Guid animalId, DateOnly? today = null, CancellationToken cancellationToken = default);

    /// <summary>Advice for every animal on a farm (optionally one batch or one animal).</summary>
    Task<IReadOnlyList<SellOrKeepAdvice>> AdviseFarmAsync(
        Guid farmId,
        DateOnly? today = null,
        Guid? batchId = null,
        Guid? animalId = null,
        bool includeProjection = false,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IAnimalSellOrKeepAdvisor"/>
/// <remarks>
/// Reads everything a farm needs in a handful of set-based calls (animals, weigh-ins, cost books, feed
/// eaten in the last 30 days, labour/overhead shares and treatments in the last 90 days) and hands each
/// animal to <see cref="SellOrKeepCalculator"/>. Cost to date is <see cref="IAnimalValuationService.CostToDate"/>,
/// the same figure every other report prints.
/// </remarks>
public sealed class AnimalSellOrKeepAdvisor : IAnimalSellOrKeepAdvisor
{
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IAnimalBatchRepository _batches;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalOverheadAllocationRepository _overheads;
    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IVaccinationRepository _vaccinations;
    private readonly IInventoryItemRepository _items;
    private readonly IAnimalValuationService _valuation;
    private readonly IFarmClock _clock;

    public AnimalSellOrKeepAdvisor(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IAnimalBatchRepository batches,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed,
        IAnimalOverheadAllocationRepository overheads,
        IMedicalTreatmentRepository treatments,
        IVaccinationRepository vaccinations,
        IInventoryItemRepository items,
        IAnimalValuationService valuation,
        IFarmClock? clock = null,
        SellOrKeepAssumptions? assumptions = null)
    {
        _animals = animals;
        _breeds = breeds;
        _batches = batches;
        _ledgers = ledgers;
        _feed = feed;
        _overheads = overheads;
        _treatments = treatments;
        _vaccinations = vaccinations;
        _items = items;
        _valuation = valuation;
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        Assumptions = assumptions ?? SellOrKeepAssumptions.Default;
    }

    public SellOrKeepAssumptions Assumptions { get; }

    public async Task<SellOrKeepAdvice?> AdviseAnimalAsync(Guid animalId, DateOnly? today = null, CancellationToken cancellationToken = default)
    {
        var animal = await _animals.GetByIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null || !AnimalStatusGroups.IsOnFarm(animal.Status))
            return null;

        var list = await AdviseFarmAsync(animal.FarmId, today, null, animal.Id, includeProjection: true, cancellationToken).ConfigureAwait(false);
        return list.Count > 0 ? list[0] : null;
    }

    public async Task<IReadOnlyList<SellOrKeepAdvice>> AdviseFarmAsync(
        Guid farmId,
        DateOnly? today = null,
        Guid? batchId = null,
        Guid? animalId = null,
        bool includeProjection = false,
        CancellationToken cancellationToken = default)
    {
        var day = today ?? _clock.Today;
        var a = Assumptions;

        var all = await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false);
        var targets = all
            .Where(x => AnimalStatusGroups.IsOnFarm(x.Status) && x.AcquisitionDate <= day)
            .Where(x => batchId is null || x.BatchId == batchId)
            .Where(x => animalId is null || x.Id == animalId)
            .ToList();
        if (targets.Count == 0)
            return [];

        var ids = targets.Select(x => x.Id).ToHashSet();
        var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(b => b.Id);

        // Weighings, oldest first.
        var earliest = targets.Min(x => x.AcquisitionDate).AddDays(-1);
        var weighIns = (await _animals.GetWeighInsByFarmAsync(farmId, earliest, day, cancellationToken).ConfigureAwait(false))
            .Where(w => ids.Contains(w.AnimalId))
            .GroupBy(w => w.AnimalId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SellOrKeepWeighing>)g.Select(w => new SellOrKeepWeighing(w.Date, w.WeightKg)).ToList());

        // Cost book (and lifetime feed where an animal has none yet).
        var ledgers = (await _ledgers.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false))
            .GroupBy(l => l.AnimalId)
            .ToDictionary(g => g.Key, g => g.First());
        var lifetimeFeed = new Dictionary<Guid, decimal>();
        if (targets.Any(t => !ledgers.ContainsKey(t.Id)))
        {
            foreach (var t in await _feed.GetTotalsForFarmAsync(farmId, earliest, day, cancellationToken).ConfigureAwait(false))
                lifetimeFeed[t.AnimalId] = t.TotalCostBdt;
        }

        // Feed eaten in the last 30 days.
        var feedFrom = day.AddDays(-(a.FeedWindowDays - 1));
        var feedWindow = (await _feed.GetDailyEatenByFarmAsync(farmId, feedFrom, day, cancellationToken).ConfigureAwait(false))
            .Where(f => ids.Contains(f.AnimalId))
            .GroupBy(f => f.AnimalId)
            .ToDictionary(g => g.Key, g => g.Sum(f => f.CostBdt));

        // Labour & overhead shares in the last 90 days, pro-rated to the days inside the window.
        var healthFrom = day.AddDays(-(a.HealthWindowDays - 1));
        var labour = new Dictionary<Guid, (decimal Amount, HashSet<int> Days)>();
        foreach (var s in await _overheads.GetByFarmAsync(farmId, healthFrom, day, cancellationToken).ConfigureAwait(false))
        {
            if (!ids.Contains(s.AnimalId) || s.AllocatedAmountBdt == 0m)
                continue;
            var start = s.PeriodStart > healthFrom ? s.PeriodStart : healthFrom;
            var end = s.PeriodEnd < day ? s.PeriodEnd : day;
            if (end < start)
                continue;
            var periodDays = Math.Max(1, s.PeriodEnd.DayNumber - s.PeriodStart.DayNumber + 1);
            var inside = end.DayNumber - start.DayNumber + 1;
            var entry = labour.TryGetValue(s.AnimalId, out var e) ? e : (0m, new HashSet<int>());
            for (var n = start.DayNumber; n <= end.DayNumber; n++)
                entry.Item2.Add(n);
            labour[s.AnimalId] = (entry.Item1 + s.AllocatedAmountBdt * inside / periodDays, entry.Item2);
        }

        // Treatments and vaccinations in the last 90 days, at the dearer of the recorded cost and stock value.
        var treatments = (await _treatments.GetByFarmStartedBetweenAsync(farmId, healthFrom, day, cancellationToken).ConfigureAwait(false))
            .Where(t => ids.Contains(t.AnimalId))
            .ToList();
        var vaccinations = (await _vaccinations.GetAdministeredByFarmAsync(farmId, healthFrom, day, cancellationToken).ConfigureAwait(false))
            .Select(v => v.Event)
            .Where(v => ids.Contains(v.AnimalId))
            .ToList();
        var unitCost = new Dictionary<Guid, decimal>();
        if (treatments.Any(t => t.InventoryItemId.HasValue) || vaccinations.Any(v => v.InventoryItemId.HasValue))
        {
            foreach (var item in await _items.GetByFarmIdAsync(farmId, null, cancellationToken).ConfigureAwait(false))
                unitCost[item.Id] = item.WeightedAverageCostBdt;
        }

        var health = new Dictionary<Guid, (decimal Cost, int Treatments)>();
        foreach (var t in treatments)
        {
            var stock = t.InventoryItemId is { } item && t.ConsumptionQuantity is > 0m
                ? t.ConsumptionQuantity.Value * unitCost.GetValueOrDefault(item)
                : 0m;
            var current = health.GetValueOrDefault(t.AnimalId);
            health[t.AnimalId] = (current.Cost + Math.Max(t.CostBdt, stock), current.Treatments + 1);
        }

        foreach (var v in vaccinations)
        {
            var dose = v.InventoryItemId is { } item && v.DosageQuantity is > 0m
                ? v.DosageQuantity.Value * unitCost.GetValueOrDefault(item)
                : 0m;
            var current = health.GetValueOrDefault(v.AnimalId);
            health[v.AnimalId] = (current.Cost + dose, current.Treatments);
        }

        // The farm's own sales in the last 12 months, by breed: price per kg = Σ price ÷ Σ sale weight.
        var salesFrom = day.AddDays(-a.OwnSalesLookbackDays);
        var ownSales = all
            .Where(x => x.Status == AnimalStatus.Sold && x.SaleDate is { } d && d >= salesFrom && d <= day
                        && x.SalePriceBdt is > 0m && x.SaleWeightKg is > 0m)
            .GroupBy(x => x.BreedId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), PerKg: g.Sum(x => x.SalePriceBdt!.Value) / g.Sum(x => x.SaleWeightKg!.Value)));

        // Recent deaths in the same batch.
        var batchDeaths = all
            .Where(x => x.Status == AnimalStatus.Dead && x.BatchId is not null && x.DeathDate is { } d && d >= healthFrom && d <= day)
            .GroupBy(x => x.BatchId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var batchNames = new Dictionary<Guid, string>();
        foreach (var id in targets.Where(x => x.BatchId is not null).Select(x => x.BatchId!.Value).Distinct())
        {
            if (await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is { } b)
                batchNames[id] = b.Name;
        }

        var meat = _valuation.MeatPricePerKgBdt;
        var dressing = _valuation.DressingRatio;
        var liveRate = _valuation.LiveWeightRatePerKgBdt;
        var usesLive = _valuation.Policy == AnimalValuationPolicy.LiveWeightMarket && liveRate != Math.Round(meat * dressing, 2);

        var result = new List<SellOrKeepAdvice>(targets.Count);
        foreach (var animal in targets)
        {
            breeds.TryGetValue(animal.BreedId, out var breed);
            ledgers.TryGetValue(animal.Id, out var ledger);
            var cost = _valuation.CostToDate(animal, ledger, lifetimeFeed.GetValueOrDefault(animal.Id));
            var sales = ownSales.TryGetValue(animal.BreedId, out var s) ? s : (Count: 0, PerKg: 0m);
            var price = SellOrKeepCalculator.ResolvePrice(
                sales.PerKg, sales.Count, breed?.ExpectedPricePerKgBdt, usesLive, liveRate, meat, dressing, a);
            var lab = labour.TryGetValue(animal.Id, out var l) ? l : (Amount: 0m, Days: new HashSet<int>());
            var h = health.GetValueOrDefault(animal.Id);

            var input = new SellOrKeepInput
            {
                AnimalId = animal.Id,
                TagId = animal.Tag.TagId,
                BreedName = breed?.Name ?? animal.Species.ToString(),
                BatchId = animal.BatchId,
                BatchName = animal.BatchId is { } bid && batchNames.TryGetValue(bid, out var bn) ? bn : null,
                AcquisitionDate = animal.AcquisitionDate,
                Today = day,
                Weighings = weighIns.TryGetValue(animal.Id, out var w) ? w : FallbackWeighing(animal),
                Cost = cost,
                FeedCostInWindowBdt = feedWindow.GetValueOrDefault(animal.Id),
                LabourOverheadInWindowBdt = lab.Amount,
                LabourOverheadDaysCovered = lab.Days.Count,
                HealthCostInWindowBdt = h.Cost,
                TreatmentsInWindow = h.Treatments,
                BatchDeathsInWindow = animal.BatchId is { } b2 ? batchDeaths.GetValueOrDefault(b2) : 0,
                BreedTargetAdgKg = BreedTarget(breed),
                Price = price,
            };

            result.Add(SellOrKeepCalculator.Advise(input, a, includeProjection));
        }

        return result;
    }

    /// <summary>
    /// The breed's growth target: the middle of its standard ADG range, else its good-commercial-farm
    /// ADG, else its average-farm ADG. Null when the breed has none.
    /// </summary>
    public static decimal? BreedTarget(Breed? breed)
    {
        if (breed is null)
            return null;
        if (breed.StandardAdgMin > 0m && breed.StandardAdgMax > 0m)
            return Math.Round((breed.StandardAdgMin + breed.StandardAdgMax) / 2m, 3);
        if (breed.AdgGoodCommercialFarm > 0m)
            return breed.AdgGoodCommercialFarm;
        return breed.AdgAverageFarm > 0m ? breed.AdgAverageFarm : null;
    }

    private static IReadOnlyList<SellOrKeepWeighing> FallbackWeighing(Animal animal) =>
        animal.LatestWeightKg is > 0m && animal.LatestWeightDate is { } d
            ? [new SellOrKeepWeighing(d, animal.LatestWeightKg.Value)]
            : [];
}
