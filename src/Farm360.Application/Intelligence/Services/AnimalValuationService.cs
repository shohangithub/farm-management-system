using System;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Intelligence.Pricing;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Intelligence.Services;

/// <inheritdoc cref="IAnimalValuationService"/>
public sealed class AnimalValuationService : IAnimalValuationService
{
    // The rule defaults, used only when configuration holds an impossible value (zero price,
    // dressing above 100 %). Read from FarmBusinessRules so the numbers live in one place.
    private static readonly FarmBusinessRules Defaults = new();

    private readonly IFarmBusinessRulesProvider _rules;

    public AnimalValuationService(IFarmBusinessRulesProvider rules)
    {
        _rules = rules;
    }

    private FarmBusinessRules Rules => _rules.Current ?? Defaults;

    public AnimalValuationPolicy Policy => Rules.Valuation;

    public decimal MeatPricePerKgBdt =>
        Rules.DefaultMeatPricePerKgBdt > 0 ? Rules.DefaultMeatPricePerKgBdt : Defaults.DefaultMeatPricePerKgBdt;

    public decimal DressingRatio =>
        Rules.DefaultDressingPercentage is > 0 and <= 1 ? Rules.DefaultDressingPercentage : Defaults.DefaultDressingPercentage;

    /// <summary>The farm rule's price (step 3 of the price resolver) under the ambient rules.</summary>
    private AnimalPrice FarmRule => AnimalPriceResolution.FarmRule(Rules);

    public decimal ByProductsPercent => FarmRule.ByProductsPercent;

    public decimal ByProductsPerHeadBdt => FarmRule.ByProductsPerHeadBdt;

    public decimal LiveWeightRatePerKgBdt => FarmRule.PricePerKgBdt;

    public decimal PerHeadAdditionBdt => FarmRule.PerHeadBdt;

    public string RateBasisDescription => FarmRule.ExplanationEn;

    public decimal PurchaseCostBdt(Animal animal, AnimalCostLedger? ledger)
    {
        ArgumentNullException.ThrowIfNull(animal);
        if (ledger is { AcquisitionCostBdt: > 0 })
        {
            return ledger.AcquisitionCostBdt;
        }

        return animal.AcquisitionPriceBdt ?? 0m;
    }

    public AnimalCostBreakdown CostToDate(Animal animal, AnimalCostLedger? ledger, decimal fallbackFeedCostBdt = 0m)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var purchase = PurchaseCostBdt(animal, ledger);

        if (ledger is null)
        {
            return new AnimalCostBreakdown(purchase, Math.Max(0m, fallbackFeedCostBdt), 0m, 0m, 0m);
        }

        return new AnimalCostBreakdown(
            purchase,
            ledger.TotalFeedCostBdt,
            ledger.TotalVetCostBdt,
            ledger.TotalLaborCostBdt,
            ledger.TotalOverheadBdt);
    }

    public decimal ValueAtWeight(decimal liveWeightKg, decimal costBasisBdt, decimal? ratePerKgOverride = null)
    {
        if (ratePerKgOverride is > 0)
        {
            return Math.Round(Math.Max(0m, liveWeightKg) * ratePerKgOverride.Value, 2);
        }

        if (Rules.Valuation == AnimalValuationPolicy.CostBasis)
        {
            return Math.Round(costBasisBdt, 2);
        }

        return FarmRule.MarketValueAt(liveWeightKg);
    }

    public decimal CurrentValue(Animal animal, AnimalCostLedger? ledger, decimal costToDateBdt, decimal? ratePerKgOverride = null) =>
        CurrentValue(animal, ledger, costToDateBdt, FarmRule, ratePerKgOverride);

    public decimal CurrentValue(Animal animal, AnimalCostLedger? ledger, decimal costToDateBdt, AnimalPrice price, decimal? ratePerKgOverride = null)
    {
        ArgumentNullException.ThrowIfNull(animal);
        ArgumentNullException.ThrowIfNull(price);

        if (AnimalStatusGroups.IsOnFarm(animal.Status))
        {
            return price.ValueAt(animal.LatestWeightKg ?? 0m, costToDateBdt, ratePerKgOverride);
        }

        return animal.Status switch
        {
            AnimalStatus.Sold or AnimalStatus.Slaughtered =>
                ledger?.SaleRevenueBdt ?? animal.SalePriceBdt ?? 0m,
            _ => 0m,
        };
    }

    public bool IsOnHand(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return AnimalStatusGroups.IsOnFarm(animal.Status);
    }
}
