using System.Globalization;
using Farm360.Domain.BusinessRules;

namespace Farm360.Application.Intelligence.Pricing;

/// <summary>
/// The price rules, pure and database-free so each step can be checked by hand. <see cref="AnimalPriceResolver"/>
/// gathers the inputs.
/// </summary>
/// <remarks>
/// Order:
/// <list type="number">
/// <item>the farm's own sales of the same breed in the last <see cref="OwnSalesLookbackDays"/> days (at least
/// <see cref="MinOwnSales"/>): Σ sale price ÷ Σ sale weight;</item>
/// <item>the breed's expected price per kg;</item>
/// <item>the farm rule by valuation method: live weight → the live-weight price; meat yield → meat × dressing ×
/// (1 + by-products %) per kg, plus by-products per head on the total; cost → no market price.</item>
/// </list>
/// </remarks>
public static class AnimalPriceResolution
{
    /// <summary>Own sales count when sold within this many days.</summary>
    public const int OwnSalesLookbackDays = 365;

    /// <summary>Own sales of the breed needed before their average is used.</summary>
    public const int MinOwnSales = 2;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly FarmBusinessRules Fallback = new();

    /// <summary>Resolves the price for one breed.</summary>
    public static AnimalPrice Resolve(
        decimal? ownSalesPricePerKg,
        int ownSalesCount,
        decimal? breedExpectedPricePerKg,
        string? breedName,
        FarmBusinessRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var breed = string.IsNullOrWhiteSpace(breedName) ? "this breed" : breedName;
        var breedBn = string.IsNullOrWhiteSpace(breedName) ? "এই জাতের" : breedName;
        var farm = FarmRule(rules);

        if (ownSalesPricePerKg is > 0m && ownSalesCount >= MinOwnSales)
        {
            var p = Math.Round(ownSalesPricePerKg.Value, 2);
            return farm with
            {
                PricePerKgBdt = p,
                Source = AnimalPriceSource.OwnSales,
                SalesCount = ownSalesCount,
                PerHeadBdt = 0m,
                BreedName = breedName ?? string.Empty,
                ExplanationEn = $"your own sales of {ownSalesCount} {breed} in the last 12 months, average {Tk(p)}/kg",
                ExplanationBn = $"গত ১২ মাসে আপনার নিজের {ownSalesCount}টি {breedBn} বিক্রির গড় {Tk(p)}/কেজি",
            };
        }

        if (breedExpectedPricePerKg is > 0m)
        {
            var p = breedExpectedPricePerKg.Value;
            return farm with
            {
                PricePerKgBdt = p,
                Source = AnimalPriceSource.BreedExpected,
                SalesCount = 0,
                PerHeadBdt = 0m,
                BreedName = breedName ?? string.Empty,
                ExplanationEn = $"breed price {Tk(p)}/kg",
                ExplanationBn = $"জাতের দাম {Tk(p)}/কেজি",
            };
        }

        return farm with { BreedName = breedName ?? string.Empty };
    }

    /// <summary>
    /// Step 3 alone: the farm rule's price. Under <see cref="AnimalValuationPolicy.CostBasis"/> the result is
    /// marked <see cref="AnimalPriceSource.CostBasis"/> but still carries the meat-yield estimate per kg.
    /// </summary>
    public static AnimalPrice FarmRule(FarmBusinessRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var meat = rules.DefaultMeatPricePerKgBdt > 0m ? rules.DefaultMeatPricePerKgBdt : Fallback.DefaultMeatPricePerKgBdt;
        var dressing = rules.DefaultDressingPercentage is > 0m and <= 1m ? rules.DefaultDressingPercentage : Fallback.DefaultDressingPercentage;
        var pct = Math.Clamp(rules.ByProductsPercentOfMeat, 0m, 0.5m);
        var perHead = Math.Max(0m, rules.ByProductsValuePerHeadBdt);
        var meatRate = MeatYieldRatePerKg(meat, dressing, pct);

        var basis = new AnimalPrice(meatRate, AnimalPriceSource.MeatAndDressing, 0, perHead)
        {
            MeatPricePerKgBdt = meat,
            DressingRatio = dressing,
            ByProductsPercent = pct,
            ByProductsPerHeadBdt = perHead,
        };

        if (rules.Valuation == AnimalValuationPolicy.LiveWeightMarket && rules.DefaultLiveWeightPricePerKgBdt > 0m)
        {
            var live = rules.DefaultLiveWeightPricePerKgBdt;
            return basis with
            {
                PricePerKgBdt = live,
                Source = AnimalPriceSource.FarmLiveRate,
                PerHeadBdt = 0m,
                ExplanationEn = $"live-weight price {Tk(live)}/kg (farm rule)",
                ExplanationBn = $"জীবিত ওজনের দাম {Tk(live)}/কেজি (খামারের নিয়ম)",
            };
        }

        var (en, bn) = MeatYieldText(meat, dressing, pct, perHead);
        if (rules.Valuation == AnimalValuationPolicy.CostBasis)
        {
            return basis with
            {
                Source = AnimalPriceSource.CostBasis,
                ExplanationEn = $"valued at cost (farm rule); market estimate {en}",
                ExplanationBn = $"খরচে মূল্যায়ন (খামারের নিয়ম); বাজার অনুমান {bn}",
            };
        }

        return basis with { ExplanationEn = en, ExplanationBn = bn };
    }

    /// <summary>meat × dressing × (1 + by-products %), per kg live weight, to the paisa.</summary>
    public static decimal MeatYieldRatePerKg(decimal meatPricePerKg, decimal dressingRatio, decimal byProductsPercent) =>
        Math.Round(meatPricePerKg * dressingRatio * (1m + byProductsPercent), 2);

    /// <summary>"meat ৳680 × dressing 55% + by-products 12% + ৳3,000 per head".</summary>
    public static (string En, string Bn) MeatYieldText(decimal meat, decimal dressing, decimal pct, decimal perHead)
    {
        var en = $"meat {Tk(meat)} × dressing {Pct(dressing)}";
        var bn = $"মাংস {Tk(meat)} × ড্রেসিং {Pct(dressing)}";
        if (pct > 0m)
        {
            en += $" + by-products {Pct(pct)}";
            bn += $" + উপজাত {Pct(pct)}";
        }

        if (perHead > 0m)
        {
            en += $" + {Tk(perHead)} per head";
            bn += $" + প্রতি পশু {Tk(perHead)}";
        }

        return (en, bn);
    }

    private static string Tk(decimal amount) =>
        "৳" + (amount == Math.Round(amount, 0) ? amount.ToString("#,##0", Invariant) : amount.ToString("#,##0.##", Invariant));

    private static string Pct(decimal fraction) => (fraction * 100m).ToString("0.#", Invariant) + "%";
}
