using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One animal's running cost on one day, split by ledger bucket.</summary>
public sealed class DailyAnimalCost
{
    public DailyAnimalCost(Guid animalId, DateOnly date)
    {
        AnimalId = animalId;
        Date = date;
    }

    public Guid AnimalId { get; }

    public DateOnly Date { get; }

    public decimal FeedKg { get; internal set; }

    public decimal FeedBdt { get; internal set; }

    public decimal VetBdt { get; internal set; }

    public decimal LabourBdt { get; internal set; }

    public decimal OverheadBdt { get; internal set; }

    public decimal TotalBdt => FeedBdt + VetBdt + LabourBdt + OverheadBdt;

    public bool IsEmpty => FeedKg == 0m && TotalBdt == 0m;
}

/// <summary>
/// Spreads the animal cost ledger over the calendar, so a report can show what each animal cost on
/// each day. It reads the same dated sources that fill the ledger buckets, so a period's total
/// matches what the Animal Cost Ledger report shows for running costs. The purchase price is
/// left out because it is a capital cost, not a daily one.
/// <list type="bullet">
///   <item><b>Feed</b>: confirmed or adjusted feed allocations on the day the feed was given.</item>
///   <item><b>Vet &amp; medicine</b>: treatments on their start date, at the higher of the entered cost
///   and the stock value of the medicine used. Vaccinations on the day given, at the dose's stock
///   value. Hand-entered vet expenses on their transaction date.</item>
///   <item><b>Labour and overhead</b>: each allocated share is spread evenly over the days the animal
///   was on the farm within the expense's period, so a monthly wage bill becomes a daily cost.</item>
/// </list>
/// </summary>
public sealed class DailyAnimalCostCalculator
{
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IVaccinationRepository _vaccinations;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IAnimalOverheadAllocationRepository _overheads;
    private readonly IInventoryItemRepository _items;

    public DailyAnimalCostCalculator(
        IAnimalFeedAllocationRepository feed,
        IMedicalTreatmentRepository treatments,
        IVaccinationRepository vaccinations,
        IFinancialTransactionRepository transactions,
        IAnimalOverheadAllocationRepository overheads,
        IInventoryItemRepository items)
    {
        _feed = feed;
        _treatments = treatments;
        _vaccinations = vaccinations;
        _transactions = transactions;
        _overheads = overheads;
        _items = items;
    }

    /// <summary>
    /// Day-by-day costs on a farm within <paramref name="period"/>. When
    /// <paramref name="animalFilter"/> is given, only those animals are returned.
    /// Empty days are not included.
    /// </summary>
    /// <param name="animalsById">
    /// The animals' records. These give each animal's on-farm dates for spreading labour and overhead.
    /// An animal missing from this map has its share spread over the whole expense period.
    /// </param>
    public async Task<IReadOnlyList<DailyAnimalCost>> CalculateAsync(
        Guid farmId,
        DateRange period,
        IReadOnlyDictionary<Guid, Animal> animalsById,
        IReadOnlySet<Guid>? animalFilter,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(animalsById);

        var cells = new Dictionary<(Guid AnimalId, DateOnly Date), DailyAnimalCost>();
        bool Wanted(Guid animalId) => animalFilter is null || animalFilter.Contains(animalId);
        DailyAnimalCost Cell(Guid animalId, DateOnly date)
        {
            if (!cells.TryGetValue((animalId, date), out var cell))
            {
                cell = new DailyAnimalCost(animalId, date);
                cells[(animalId, date)] = cell;
            }

            return cell;
        }

        // ── Feed ────────────────────────────────────────────────────────────
        var feedDays = await _feed.GetDailyEatenByFarmAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);
        foreach (var day in feedDays)
        {
            if (!Wanted(day.AnimalId))
            {
                continue;
            }

            var cell = Cell(day.AnimalId, day.Date);
            cell.FeedKg += day.Kg;
            cell.FeedBdt += day.CostBdt;
        }

        // ── Vet & medicine ──────────────────────────────────────────────────
        var treatments = await _treatments.GetByFarmStartedBetweenAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);
        var vaccinations = await _vaccinations.GetAdministeredByFarmAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);

        var itemIds = treatments.Where(t => t.InventoryItemId.HasValue).Select(t => t.InventoryItemId!.Value)
            .Concat(vaccinations.Where(v => v.Event.InventoryItemId.HasValue).Select(v => v.Event.InventoryItemId!.Value))
            .Distinct()
            .ToList();
        var unitCost = new Dictionary<Guid, decimal>(itemIds.Count);
        foreach (var itemId in itemIds)
        {
            var item = await _items.GetByIdAsync(itemId, cancellationToken).ConfigureAwait(false);
            unitCost[itemId] = item?.WeightedAverageCostBdt ?? 0m;
        }

        foreach (var t in treatments)
        {
            if (!Wanted(t.AnimalId))
            {
                continue;
            }

            // Same rule as TreatmentLoggedEventHandler: the animal carries the larger of the two.
            var stockValue = t.InventoryItemId is { } id && t.ConsumptionQuantity is > 0
                ? Math.Round(t.ConsumptionQuantity.Value * unitCost.GetValueOrDefault(id), 2)
                : 0m;
            var cost = Math.Max(t.CostBdt, stockValue);
            if (cost > 0m)
            {
                Cell(t.AnimalId, t.StartDate).VetBdt += cost;
            }
        }

        foreach (var (vaccination, _) in vaccinations)
        {
            if (!Wanted(vaccination.AnimalId)
                || vaccination.AdministeredDate is not { } given
                || vaccination.InventoryItemId is not { } id
                || vaccination.DosageQuantity is not > 0)
            {
                continue;
            }

            var cost = Math.Round(vaccination.DosageQuantity.Value * unitCost.GetValueOrDefault(id), 2);
            if (cost > 0m)
            {
                Cell(vaccination.AnimalId, given).VetBdt += cost;
            }
        }

        var manualVet = await _transactions.GetManualVetPostingsAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);
        foreach (var tx in manualVet)
        {
            if (tx.LedgerVetPostedAnimalId is not { } animalId || tx.LedgerVetPostedBdt is not { } amount || !Wanted(animalId))
            {
                continue;
            }

            Cell(animalId, DateOnly.FromDateTime(tx.TransactionDate)).VetBdt += amount;
        }

        // ── Labour & overhead, spread over the days each share covers ───────
        var allocations = await _overheads.GetByFarmAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);
        foreach (var share in allocations)
        {
            if (!Wanted(share.AnimalId) || share.AllocatedAmountBdt == 0m)
            {
                continue;
            }

            animalsById.TryGetValue(share.AnimalId, out var animal);
            var days = DaysOnFarm(animal, share.PeriodStart, share.PeriodEnd);
            var labour = share.Bucket == Domain.Finance.OverheadBucket.Labour;

            for (var i = 0; i < days.Count; i++)
            {
                var date = days[i];
                if (!period.Contains(date))
                {
                    continue;
                }

                var amount = SpreadAmount(share.AllocatedAmountBdt, days.Count, i);
                var cell = Cell(share.AnimalId, date);
                if (labour)
                {
                    cell.LabourBdt += amount;
                }
                else
                {
                    cell.OverheadBdt += amount;
                }
            }
        }

        return cells.Values
            .Where(c => !c.IsEmpty)
            .OrderBy(c => c.Date)
            .ThenBy(c => c.AnimalId)
            .ToList();
    }

    /// <summary>
    /// The first and last day the animal is counted on the farm: from acquisition until the day
    /// before its sale or death (the farm rule: an animal is not on the farm on its exit date, the
    /// same rule feeding and the overhead allocation use). An animal that left with no recorded
    /// date was on the farm on no day we can name, so its span is empty.
    /// </summary>
    public static (DateOnly From, DateOnly To) OnFarmSpan(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        if (animal.ExitDate is { } exit)
            return (animal.AcquisitionDate, exit.AddDays(-1));

        return animal.HasLeftFarm
            ? (animal.AcquisitionDate, animal.AcquisitionDate.AddDays(-1))
            : (animal.AcquisitionDate, DateOnly.MaxValue);
    }

    /// <summary>Is the animal on the farm on <paramref name="date"/>?</summary>
    public static bool IsOnFarm(Animal animal, DateOnly date)
    {
        var (from, to) = OnFarmSpan(animal);
        return date >= from && date <= to;
    }

    /// <summary>
    /// The days in [<paramref name="from"/>, <paramref name="to"/>] that the animal was on the farm.
    /// If none qualify, or the animal is unknown, every day in the period is used, so that the
    /// whole amount is still shown.
    /// </summary>
    private static List<DateOnly> DaysOnFarm(Animal? animal, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        var all = new List<DateOnly>(to.DayNumber - from.DayNumber + 1);
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            all.Add(d);
        }

        if (animal is null)
        {
            return all;
        }

        var present = all.Where(d => IsOnFarm(animal, d)).ToList();
        return present.Count > 0 ? present : all;
    }

    /// <summary>An even share per day, rounded to paisa. The last day takes the rounding remainder, so the days add up to the full amount.</summary>
    private static decimal SpreadAmount(decimal total, int days, int index)
    {
        if (days <= 1)
        {
            return total;
        }

        var perDay = Math.Round(total / days, 2, MidpointRounding.AwayFromZero);
        return index == days - 1 ? total - (perDay * (days - 1)) : perDay;
    }
}
