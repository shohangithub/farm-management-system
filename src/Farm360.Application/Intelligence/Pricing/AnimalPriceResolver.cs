using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Intelligence.Pricing;

/// <summary>
/// The one answer to "what does this live animal sell for?". The Sell-or-Keep advisor, the animal page's
/// "Profit if sold today" card, the herd and animal reports and the batch P&amp;L all ask it, so the same
/// animal is priced the same way everywhere. See <see cref="AnimalPriceResolution"/> for the order.
/// </summary>
/// <remarks>
/// Market and sale-value displays only. The balance sheet keeps valuing livestock at cost (plus carried
/// feed and care) and does not use this.
/// </remarks>
public interface IAnimalPriceResolver
{
    /// <summary>The price of one animal, with its farm's own sales and rules.</summary>
    Task<AnimalPrice> ForAnimalAsync(Animal animal, DateOnly? today = null, CancellationToken cancellationToken = default);

    /// <summary>Prices for every breed on a farm, loaded in two queries.</summary>
    Task<AnimalPriceBook> ForFarmAsync(Guid farmId, DateOnly? today = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Prices for a farm from data the caller already holds (every animal of the farm, sold ones included,
    /// and the breeds). No database access.
    /// </summary>
    AnimalPriceBook FromLoaded(Guid tenantId, Guid farmId, IEnumerable<Animal> farmAnimals, IReadOnlyDictionary<Guid, Breed> breeds, DateOnly today);
}

/// <summary>A farm's prices by breed. <see cref="For(Animal)"/> never fails: an unknown breed gets the farm rule.</summary>
public sealed class AnimalPriceBook
{
    private readonly Dictionary<Guid, AnimalPrice> _byBreed = new();
    private readonly IReadOnlyDictionary<Guid, (int Count, decimal PerKg)> _ownSales;
    private readonly IReadOnlyDictionary<Guid, Breed> _breeds;

    internal AnimalPriceBook(
        FarmBusinessRules rules,
        IReadOnlyDictionary<Guid, (int Count, decimal PerKg)> ownSales,
        IReadOnlyDictionary<Guid, Breed> breeds)
    {
        Rules = rules;
        _ownSales = ownSales;
        _breeds = breeds;
        FarmRule = AnimalPriceResolution.FarmRule(rules);
    }

    /// <summary>The rules the prices were resolved under (the farm's own overrides applied).</summary>
    public FarmBusinessRules Rules { get; }

    /// <summary>The farm rule's price alone (step 3), as reports print it in their parameter help.</summary>
    public AnimalPrice FarmRule { get; }

    /// <summary>The price for a breed.</summary>
    public AnimalPrice For(Guid breedId)
    {
        if (_byBreed.TryGetValue(breedId, out var cached))
            return cached;

        _breeds.TryGetValue(breedId, out var breed);
        var sales = _ownSales.TryGetValue(breedId, out var s) ? s : (Count: 0, PerKg: 0m);
        var price = AnimalPriceResolution.Resolve(sales.PerKg, sales.Count, breed?.ExpectedPricePerKgBdt, breed?.Name, Rules);
        _byBreed[breedId] = price;
        return price;
    }

    /// <summary>The price for an animal (by its breed).</summary>
    public AnimalPrice For(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return For(animal.BreedId);
    }
}

/// <inheritdoc cref="IAnimalPriceResolver"/>
public sealed class AnimalPriceResolver : IAnimalPriceResolver
{
    private static readonly FarmBusinessRules Defaults = new();

    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IFarmBusinessRulesProvider _rules;
    private readonly IFarmClock _clock;

    public AnimalPriceResolver(IAnimalRepository animals, IBreedRepository breeds, IFarmBusinessRulesProvider rules, IFarmClock? clock = null)
    {
        _animals = animals;
        _breeds = breeds;
        _rules = rules;
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
    }

    public async Task<AnimalPrice> ForAnimalAsync(Animal animal, DateOnly? today = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var book = await LoadAsync(animal.TenantId, animal.FarmId, today, cancellationToken).ConfigureAwait(false);
        return book.For(animal);
    }

    public Task<AnimalPriceBook> ForFarmAsync(Guid farmId, DateOnly? today = null, CancellationToken cancellationToken = default) =>
        LoadAsync(null, farmId, today, cancellationToken);

    public AnimalPriceBook FromLoaded(Guid tenantId, Guid farmId, IEnumerable<Animal> farmAnimals, IReadOnlyDictionary<Guid, Breed> breeds, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(farmAnimals);
        ArgumentNullException.ThrowIfNull(breeds);

        var from = today.AddDays(-AnimalPriceResolution.OwnSalesLookbackDays);
        var ownSales = farmAnimals
            .Where(x => x.FarmId == farmId && x.Status == AnimalStatus.Sold && x.SaleDate is { } d && d >= from && d <= today
                        && x.SalePriceBdt is > 0m && x.SaleWeightKg is > 0m)
            .GroupBy(x => x.BreedId)
            .ToDictionary(g => g.Key, g => (Count: g.Count(), PerKg: g.Sum(x => x.SalePriceBdt!.Value) / g.Sum(x => x.SaleWeightKg!.Value)));

        return new AnimalPriceBook(RulesFor(tenantId, farmId), ownSales, breeds);
    }

    private async Task<AnimalPriceBook> LoadAsync(Guid? tenantId, Guid farmId, DateOnly? today, CancellationToken cancellationToken)
    {
        var day = today ?? _clock.Today;
        var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false) ?? [];
        var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false) ?? [])
            .GroupBy(b => b.Id)
            .ToDictionary(g => g.Key, g => g.First());
        var tenant = tenantId ?? (animals.Count > 0 ? animals[0].TenantId : Guid.Empty);
        return FromLoaded(tenant, farmId, animals, breeds, day);
    }

    /// <summary>The farm's own rules (per-farm overrides applied), falling back to the ambient rules.</summary>
    private FarmBusinessRules RulesFor(Guid tenantId, Guid farmId)
    {
        FarmBusinessRules? rules = null;
        if (tenantId != Guid.Empty)
            rules = _rules.For(tenantId, farmId);
        return rules ?? _rules.Current ?? Defaults;
    }
}
