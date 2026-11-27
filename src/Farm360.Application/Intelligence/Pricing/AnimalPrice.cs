namespace Farm360.Application.Intelligence.Pricing;

/// <summary>Where a live animal's price came from, in priority order.</summary>
/// <remarks>The names are part of the API (the web reads them), so they are kept stable.</remarks>
public enum AnimalPriceSource
{
    /// <summary>Average live price per kg of the farm's own sales of the same breed in the last 12 months (at least two).</summary>
    OwnSales = 0,

    /// <summary>The expected price per kg set on the breed.</summary>
    BreedExpected = 1,

    /// <summary>The farm rule's live-weight price (valuation method "live weight × live-weight price").</summary>
    FarmLiveRate = 2,

    /// <summary>The farm rule's meat price × dressing % (+ by-products), valuation method "meat yield".</summary>
    MeatAndDressing = 3,

    /// <summary>
    /// The farm values animals at cost: there is no market price. <see cref="AnimalPrice.PricePerKgBdt"/>
    /// still carries the meat-yield estimate for forward-looking tools (sell or keep, projections).
    /// </summary>
    CostBasis = 4,
}

/// <summary>
/// What one live animal sells for: a price per kg of live weight, plus a fixed amount per head (the
/// by-products under the meat-yield method), where it came from, and a one-line explanation.
/// </summary>
/// <param name="PricePerKgBdt">Taka per kg live weight.</param>
/// <param name="Source">Where the price came from.</param>
/// <param name="SalesCount">How many own sales the average is built on (<see cref="AnimalPriceSource.OwnSales"/> only).</param>
/// <param name="PerHeadBdt">Added once to the animal's value (by-products sold separately); 0 unless meat-yield.</param>
public sealed record AnimalPrice(
    decimal PricePerKgBdt,
    AnimalPriceSource Source,
    int SalesCount = 0,
    decimal PerHeadBdt = 0m)
{
    /// <summary>The breed the price was resolved for.</summary>
    public string BreedName { get; init; } = string.Empty;

    /// <summary>Farm meat price per kg (the meat-yield inputs, for display).</summary>
    public decimal MeatPricePerKgBdt { get; init; }

    /// <summary>Dressing as a 0–1 fraction.</summary>
    public decimal DressingRatio { get; init; }

    /// <summary>By-products as a 0–0.5 fraction of meat value.</summary>
    public decimal ByProductsPercent { get; init; }

    /// <summary>By-products per head (taka).</summary>
    public decimal ByProductsPerHeadBdt { get; init; }

    /// <summary>"your own sales of 3 Deshi in the last 12 months, average ৳425/kg", and so on.</summary>
    public string ExplanationEn { get; init; } = string.Empty;

    /// <inheritdoc cref="ExplanationEn"/>
    public string ExplanationBn { get; init; } = string.Empty;

    /// <summary>Back from the API shape (<see cref="Farm360.Contracts.Intelligence.AnimalMarketPriceDto"/>).</summary>
    public static AnimalPrice FromDto(Farm360.Contracts.Intelligence.AnimalMarketPriceDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var source = Enum.TryParse<AnimalPriceSource>(dto.Source, out var s) ? s
            : dto.ValuedAtCost ? AnimalPriceSource.CostBasis : AnimalPriceSource.MeatAndDressing;
        return new AnimalPrice(dto.PricePerKgBdt, source, dto.SalesCount, dto.PerHeadBdt)
        {
            ExplanationEn = dto.ExplanationEn,
            ExplanationBn = dto.ExplanationBn,
        };
    }

    /// <summary>True when the farm values animals at cost (no market value shown).</summary>
    public bool ValuedAtCost => Source == AnimalPriceSource.CostBasis;

    /// <summary>Market value at a live weight: weight × price/kg + per-head addition. 0 without a weight.</summary>
    public decimal MarketValueAt(decimal liveWeightKg) =>
        liveWeightKg > 0m ? Math.Round(liveWeightKg * PricePerKgBdt + PerHeadBdt, 2) : 0m;

    /// <summary>
    /// The value shown for an animal on the farm: <paramref name="costBasisBdt"/> when the farm values
    /// at cost, else <see cref="MarketValueAt"/>. A positive <paramref name="ratePerKgOverride"/> (a
    /// what-if rate typed by the user) replaces the price and drops the per-head addition, because a
    /// typed live price already includes the by-products.
    /// </summary>
    public decimal ValueAt(decimal liveWeightKg, decimal costBasisBdt, decimal? ratePerKgOverride = null)
    {
        if (ratePerKgOverride is > 0m)
            return Math.Round(Math.Max(0m, liveWeightKg) * ratePerKgOverride.Value, 2);

        return ValuedAtCost ? Math.Round(costBasisBdt, 2) : MarketValueAt(liveWeightKg);
    }
}
