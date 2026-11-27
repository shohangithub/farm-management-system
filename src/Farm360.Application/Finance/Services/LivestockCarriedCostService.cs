using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Application.Finance.Services;

/// <summary>An animal as the carried-cost rule sees it.</summary>
/// <param name="ExitDate">Sale or death date; the animal is not on the farm on that day itself.</param>
/// <param name="HasLeftFarm">Sold, dead, slaughtered or transferred. Left with no exit date = never counted.</param>
public sealed record CarriedCostAnimal(Guid AnimalId, DateOnly AcquisitionDate, DateOnly? ExitDate, bool HasLeftFarm);

/// <summary>Growing cost (feed, health, labour, overhead) spent on one animal on one day. Never the purchase price.</summary>
public sealed record AnimalGrowingCost(Guid AnimalId, DateOnly Date, decimal AmountBdt);

/// <summary>What the carried-cost rule reads from the registers, up to a date.</summary>
public sealed record LivestockCarriedCostData(
    IReadOnlyList<CarriedCostAnimal> Animals,
    IReadOnlyList<AnimalGrowingCost> Costs);

/// <summary>A dated change in the carried cost: carried(D) = the sum of every movement dated on or before D.</summary>
public sealed record CarriedCostMovement(DateOnly Date, decimal AmountBdt);

/// <summary>
/// Reads the farm's per-animal growing costs with grouped, set-based queries (implemented in persistence).
/// </summary>
/// <remarks>
/// The three cost sources never overlap: feed actually eaten (confirmed feed allocations); labour and
/// overhead allocations (shared costs and hand-entered costs tagged to an animal); and the other
/// expense postings tagged to an animal (treatments, vaccine and medicine stock, vet fees) that are
/// neither the source of an allocation nor the farm-level feed posting.
/// </remarks>
public interface ILivestockCarriedCostSource
{
    Task<LivestockCarriedCostData> LoadAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default);
}

/// <summary>
/// The single answer to "how much feed and care is tied up in the animals still on the farm".
/// </summary>
/// <remarks>
/// <para>
/// Standard livestock-inventory accounting. The ledger books feed, health, labour and overhead as
/// expenses when they happen. The part spent on animals that are still on the farm at a date is part
/// of their value: the statements carry it forward (a lower cost now) and release it when the animal
/// is sold or dies, so it is matched with the sale.
/// </para>
/// <para>
/// carried(D) = Σ over animals on the farm at D (acquired on or before D and not sold, dead or
/// slaughtered by D) of their growing costs dated on or before D. The purchase price is never part
/// of it — it is already in the Livestock account (1300).
/// </para>
/// </remarks>
public interface ILivestockCarriedCostService
{
    /// <summary>True when the farm rule "Carry feed &amp; care costs of animals on the farm" is on.</summary>
    bool IsEnabled(Guid farmId);

    /// <summary>carried(<paramref name="asOf"/>), whatever the farm rule says.</summary>
    Task<decimal> GetCarriedCostAsync(Guid farmId, DateOnly asOf, CancellationToken cancellationToken = default);

    /// <summary>Dated changes in the carried cost up to <paramref name="upTo"/>, oldest first, one per day.</summary>
    Task<IReadOnlyList<CarriedCostMovement>> GetMovementsAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default);

    /// <summary>
    /// The profit adjustment for [from, to]: carried(to) − carried(from − 1). Positive reduces expenses.
    /// Zero when the farm rule is off.
    /// </summary>
    Task<decimal> GetPeriodAdjustmentAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed class LivestockCarriedCostService : ILivestockCarriedCostService
{
    private readonly ILivestockCarriedCostSource _source;
    private readonly IFarmBusinessRulesProvider? _rules;
    private readonly ITenantService? _tenant;

    public LivestockCarriedCostService(
        ILivestockCarriedCostSource source,
        IFarmBusinessRulesProvider? rules = null,
        ITenantService? tenant = null)
    {
        _source = source;
        _rules = rules;
        _tenant = tenant;
    }

    public bool IsEnabled(Guid farmId)
    {
        if (_rules is null)
            return new Farm360.Domain.BusinessRules.FarmBusinessRules().CarryLivestockGrowingCosts;

        var tenantId = _tenant?.TenantId ?? Guid.Empty;
        var rules = tenantId == Guid.Empty ? _rules.Current : _rules.For(tenantId, farmId);
        return rules.CarryLivestockGrowingCosts;
    }

    public async Task<decimal> GetCarriedCostAsync(Guid farmId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var movements = await GetMovementsAsync(farmId, asOf, cancellationToken).ConfigureAwait(false);
        return CarriedAt(movements, asOf);
    }

    public async Task<IReadOnlyList<CarriedCostMovement>> GetMovementsAsync(Guid farmId, DateOnly upTo, CancellationToken cancellationToken = default)
    {
        var data = await _source.LoadAsync(farmId, upTo, cancellationToken).ConfigureAwait(false);
        return BuildMovements(data, upTo);
    }

    public async Task<decimal> GetPeriodAdjustmentAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled(farmId) || to < from)
            return 0m;

        var movements = await GetMovementsAsync(farmId, to, cancellationToken).ConfigureAwait(false);
        return PeriodAdjustment(movements, from, to);
    }

    /// <summary>carried(to) − carried(from − 1).</summary>
    public static decimal PeriodAdjustment(IEnumerable<CarriedCostMovement> movements, DateOnly from, DateOnly to) =>
        movements.Where(m => m.Date >= from && m.Date <= to).Sum(m => m.AmountBdt);

    /// <summary>carried(<paramref name="asOf"/>) from the movements.</summary>
    public static decimal CarriedAt(IEnumerable<CarriedCostMovement> movements, DateOnly asOf) =>
        movements.Where(m => m.Date <= asOf).Sum(m => m.AmountBdt);

    /// <summary>
    /// Turns the costs into dated movements: each cost is carried from the later of its date and the
    /// animal's arrival; on the exit date everything carried for that animal is released. A cost dated
    /// on or after the exit date was never carried (it is a cost of that period already).
    /// </summary>
    public static IReadOnlyList<CarriedCostMovement> BuildMovements(LivestockCarriedCostData data, DateOnly upTo)
    {
        ArgumentNullException.ThrowIfNull(data);

        var animals = new Dictionary<Guid, CarriedCostAnimal>();
        foreach (var a in data.Animals)
            animals[a.AnimalId] = a;

        var byDay = new SortedDictionary<DateOnly, decimal>();
        var carriedPerAnimal = new Dictionary<Guid, decimal>();

        foreach (var cost in data.Costs)
        {
            if (cost.AmountBdt == 0m || cost.Date > upTo || !animals.TryGetValue(cost.AnimalId, out var animal))
                continue;

            // Left with no recorded exit date: not on the farm on any day, so never carried.
            if (animal.HasLeftFarm && animal.ExitDate is null)
                continue;

            var day = cost.Date > animal.AcquisitionDate ? cost.Date : animal.AcquisitionDate;
            if (animal.ExitDate is { } exit && day >= exit)
                continue;
            if (day > upTo)
                continue;

            byDay[day] = byDay.GetValueOrDefault(day) + cost.AmountBdt;
            carriedPerAnimal[cost.AnimalId] = carriedPerAnimal.GetValueOrDefault(cost.AnimalId) + cost.AmountBdt;
        }

        foreach (var (animalId, carried) in carriedPerAnimal)
        {
            if (animals[animalId].ExitDate is { } exit && exit <= upTo && carried != 0m)
                byDay[exit] = byDay.GetValueOrDefault(exit) - carried;
        }

        return byDay
            .Where(kv => kv.Value != 0m)
            .Select(kv => new CarriedCostMovement(kv.Key, kv.Value))
            .ToList();
    }

    /// <summary>
    /// Expense postings tagged to an animal that count as growing costs (the operating heads). The
    /// purchase price (cost of animals sold, mortality), finance costs, disposals and stock write-offs
    /// are never part of it.
    /// </summary>
    public static readonly TransactionCategory[] TaggedPostingCategories =
    [
        TransactionCategory.FeedCost,
        TransactionCategory.VeterinaryCost,
        TransactionCategory.MedicineCost,
        TransactionCategory.LaborCost,
        TransactionCategory.Utilities,
        TransactionCategory.Transport,
        TransactionCategory.MiscellaneousExpense,
        TransactionCategory.ConsumableExpense,
    ];

    /// <summary>The module name on automated feed postings; their per-animal cost comes from the feed allocations instead.</summary>
    public const string FeedingSourceModule = "Feeding";
}
