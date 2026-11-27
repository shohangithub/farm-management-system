using Farm360.Application.Intelligence.Pricing;
using Farm360.Application.Intelligence.Services;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>One weighing.</summary>
public sealed record SellOrKeepWeighing(DateOnly Date, decimal WeightKg);

/// <summary>What the advice is.</summary>
public enum SellOrKeepVerdict
{
    SellNow = 0,
    Keep = 1,
    LosingMoney = 2,
    WeighFirst = 3,
}

/// <summary>How far the advice can be trusted.</summary>
public enum SellOrKeepConfidence
{
    Low = 0,
    Medium = 1,
    High = 2,
}

/// <summary>Which weighings the gain per day came from.</summary>
public enum SellOrKeepGrowthSource
{
    None = 0,

    /// <summary>Least-squares slope over the recent weighings.</summary>
    Recent = 1,

    /// <summary>(last − first weighing) ÷ days between them.</summary>
    Lifetime = 2,
}

/// <summary>Where a breed's growth target came from.</summary>
public enum SellOrKeepBreedTargetBasis
{
    None = 0,

    /// <summary>The middle of the breed's standard ADG range (kg/day).</summary>
    StandardRange = 1,

    /// <summary>The breed's ADG on a good commercial farm (kg/day).</summary>
    GoodCommercialFarm = 2,

    /// <summary>The breed's ADG on an average farm (kg/day).</summary>
    AverageFarm = 3,
}

/// <summary>Where a cost-per-day figure came from.</summary>
public enum SellOrKeepCostBasis
{
    /// <summary>Nothing recorded: 0 per day.</summary>
    None = 0,

    /// <summary>The records of the last few weeks (feed 30 days, labour/overhead and health 90 days).</summary>
    RecentRecords = 1,

    /// <summary>The animal's lifetime total ÷ days on the farm (no recent records).</summary>
    LifetimeAverage = 2,

    /// <summary>Typed in as a what-if.</summary>
    Override = 3,
}

/// <summary>
/// What-if changes to the projection. Every field is optional: <c>null</c> keeps the figure worked out
/// from the animal's own records, so a plan with no overrides is exactly the "Sell or keep?" advice.
/// </summary>
public sealed record SellOrKeepOverrides
{
    /// <summary>The longest projection allowed (two years).</summary>
    public const int MaxHorizonDays = 730;

    /// <summary>Sale price per kg LIVE weight.</summary>
    public decimal? PricePerKgBdt { get; init; }

    /// <summary>Fixed amount added once per head (by-products sold separately).</summary>
    public decimal? PerHeadBdt { get; init; }

    /// <summary>Gain per day (kg) the projection starts from.</summary>
    public decimal? AdgKgPerDay { get; init; }

    /// <summary>Feed cost per day today (it still grows with the animal's weight).</summary>
    public decimal? FeedPerDayBdt { get; init; }

    /// <summary>Labour and overhead per day.</summary>
    public decimal? LabourOverheadPerDayBdt { get; init; }

    /// <summary>Treatments and vaccinations per day.</summary>
    public decimal? HealthPerDayBdt { get; init; }

    /// <summary>How many days ahead to project.</summary>
    public int? HorizonDays { get; init; }

    /// <summary>How much growth slows (percent of today's gain) over <see cref="SellOrKeepAssumptions.GrowthDeclineDays"/> days.</summary>
    public decimal? GrowthSlowdownPercent { get; init; }

    /// <summary>True when nothing is overridden.</summary>
    public bool IsEmpty =>
        PricePerKgBdt is null && PerHeadBdt is null && AdgKgPerDay is null && FeedPerDayBdt is null &&
        LabourOverheadPerDayBdt is null && HealthPerDayBdt is null && HorizonDays is null && GrowthSlowdownPercent is null;
}

/// <summary>Recent gain per day against the breed's target.</summary>
public enum SellOrKeepGrowthVsTarget
{
    Unknown = 0,
    Below = 1,
    At = 2,
    Above = 3,
}

/// <summary>The most likely reason an animal is losing money.</summary>
public enum SellOrKeepLossCause
{
    None = 0,
    Sickness = 1,
    SlowGrowth = 2,
    FeedCost = 3,
    PurchasePrice = 4,
}

/// <summary>Everything the calculator needs for one animal. Costs are in BDT.</summary>
public sealed record SellOrKeepInput
{
    public required Guid AnimalId { get; init; }
    public required string TagId { get; init; }
    public string BreedName { get; init; } = string.Empty;
    public Guid? BatchId { get; init; }
    public string? BatchName { get; init; }
    public required DateOnly AcquisitionDate { get; init; }
    public required DateOnly Today { get; init; }
    public IReadOnlyList<SellOrKeepWeighing> Weighings { get; init; } = [];

    /// <summary>Total invested so far (the animal's cost book).</summary>
    public required AnimalCostBreakdown Cost { get; init; }

    /// <summary>Feed eaten in the last <see cref="SellOrKeepAssumptions.FeedWindowDays"/> days.</summary>
    public decimal FeedCostInWindowBdt { get; init; }

    /// <summary>Labour and overhead share in the last <see cref="SellOrKeepAssumptions.HealthWindowDays"/> days.</summary>
    public decimal LabourOverheadInWindowBdt { get; init; }

    /// <summary>Days in that window covered by a labour/overhead share (0 = none booked yet).</summary>
    public int LabourOverheadDaysCovered { get; init; }

    /// <summary>Treatments and vaccinations in the last <see cref="SellOrKeepAssumptions.HealthWindowDays"/> days.</summary>
    public decimal HealthCostInWindowBdt { get; init; }

    /// <summary>Treatments started in the last <see cref="SellOrKeepAssumptions.HealthWindowDays"/> days.</summary>
    public int TreatmentsInWindow { get; init; }

    /// <summary>Deaths among animals of the same batch in the last <see cref="SellOrKeepAssumptions.HealthWindowDays"/> days.</summary>
    public int BatchDeathsInWindow { get; init; }

    /// <summary>The breed's target gain per day (kg), when set.</summary>
    public decimal? BreedTargetAdgKg { get; init; }

    /// <summary>Where <see cref="BreedTargetAdgKg"/> came from.</summary>
    public SellOrKeepBreedTargetBasis BreedTargetBasis { get; init; }

    /// <summary>The breed's standard ADG range (kg/day), when the target is its middle.</summary>
    public decimal? BreedTargetMinKg { get; init; }

    /// <inheritdoc cref="BreedTargetMinKg"/>
    public decimal? BreedTargetMaxKg { get; init; }

    /// <summary>The sale price from <see cref="IAnimalPriceResolver"/>: per kg live weight plus any per-head addition.</summary>
    public required AnimalPrice Price { get; init; }
}

/// <summary>One day of the projection.</summary>
public sealed record SellOrKeepPoint(
    int Day,
    DateOnly Date,
    decimal WeightKg,
    decimal AdgKg,
    decimal DailyCostBdt,
    decimal CostBdt,
    decimal ValueBdt,
    decimal ProfitBdt);

/// <summary>The advice for one animal, with every figure it was worked out from.</summary>
public sealed record SellOrKeepAdvice
{
    public required Guid AnimalId { get; init; }
    public required string TagId { get; init; }
    public string BreedName { get; init; } = string.Empty;
    public Guid? BatchId { get; init; }
    public string? BatchName { get; init; }
    public required DateOnly Today { get; init; }
    public int DaysOnFarm { get; init; }

    // ── Today ──
    public required AnimalCostBreakdown Cost { get; init; }
    public decimal InvestedBdt => Cost.TotalBdt;
    public int Weighings { get; init; }
    public decimal? LatestWeightKg { get; init; }
    public DateOnly? LatestWeightDate { get; init; }

    /// <summary>Latest weighing plus the growth since it (up to <see cref="SellOrKeepAssumptions.MaxExtrapolationDays"/>).</summary>
    public decimal? WeightTodayKg { get; init; }
    public decimal? ValueTodayBdt { get; init; }
    public decimal? ProfitTodayBdt { get; init; }

    // ── Growth ──
    public decimal? AdgKgPerDay { get; init; }
    public SellOrKeepGrowthSource GrowthSource { get; init; }
    public int GrowthWeighings { get; init; }
    public decimal? GrowthFitR2 { get; init; }
    public decimal? BreedTargetAdgKg { get; init; }
    public SellOrKeepBreedTargetBasis BreedTargetBasis { get; init; }
    public decimal? BreedTargetMinKg { get; init; }
    public decimal? BreedTargetMaxKg { get; init; }
    public SellOrKeepGrowthVsTarget GrowthVsTarget { get; init; }

    /// <summary>Gain per day since the first weighing: (last − first weighing) ÷ days between them.</summary>
    public decimal? LifetimeAdgKgPerDay { get; init; }

    /// <summary>The first weighing (usually the arrival weighing).</summary>
    public DateOnly? FirstWeighingDate { get; init; }

    /// <summary>Least-squares gain per day over the last <see cref="RecentGrowthWindowDays"/> days (2+ weighings), else null.</summary>
    public decimal? RecentAdgKgPerDay { get; init; }

    /// <summary>The window behind <see cref="RecentAdgKgPerDay"/>.</summary>
    public int RecentGrowthWindowDays { get; init; }

    /// <summary>The gain per day the projection starts from: the typed what-if, else <see cref="AdgKgPerDay"/>.</summary>
    public decimal? ProjectionAdgKgPerDay { get; init; }

    /// <summary>The projection's gain per day was typed in.</summary>
    public bool AdgOverridden { get; init; }

    /// <summary>Growth slows by this percent of today's gain over <see cref="GrowthDeclineDays"/> days.</summary>
    public decimal GrowthSlowdownPercent { get; init; }

    /// <summary>Days over which <see cref="GrowthSlowdownPercent"/> is reached.</summary>
    public int GrowthDeclineDays { get; init; }

    // ── Cost run-rate ──
    public decimal FeedPerDayBdt { get; init; }
    public decimal LabourOverheadPerDayBdt { get; init; }
    public decimal HealthPerDayBdt { get; init; }
    public decimal DailyCostBdt => FeedPerDayBdt + LabourOverheadPerDayBdt + HealthPerDayBdt;
    public SellOrKeepCostBasis FeedCostBasis { get; init; }
    public SellOrKeepCostBasis LabourOverheadCostBasis { get; init; }
    public SellOrKeepCostBasis HealthCostBasis { get; init; }

    /// <summary>Feed per day is averaged over this many days.</summary>
    public int FeedWindowDays { get; init; }

    /// <summary>Labour, overhead and health per day are averaged over this many days.</summary>
    public int HealthWindowDays { get; init; }

    /// <summary>Total cost per kg gained: today's cost per day ÷ the projection's gain per day.</summary>
    public decimal? CostPerKgGainBdt { get; init; }

    /// <summary>Feed cost per kg gained (feed per day ÷ gain per day), when it grows.</summary>
    public decimal? FeedCostPerKgGainBdt { get; init; }

    // ── Price ──
    /// <summary>The same price the animal page's "Profit if sold today" card uses (<see cref="IAnimalPriceResolver"/>).</summary>
    public required AnimalPrice Price { get; init; }

    /// <summary>The price per kg or per-head addition was typed in (a what-if).</summary>
    public bool PriceOverridden { get; init; }

    // ── Projection ──
    /// <summary>How many days ahead the projection looks.</summary>
    public int HorizonDays { get; init; }

    public int? BestDay { get; init; }
    public DateOnly? BestDate { get; init; }
    public decimal? BestWeightKg { get; init; }
    public decimal? ProfitAtBestDayBdt { get; init; }
    public decimal? ExtraProfitBdt => ProfitAtBestDayBdt - ProfitTodayBdt;
    public int? BreakEvenDay { get; init; }
    public DateOnly? BreakEvenDate { get; init; }

    /// <summary>Profit at the best day ÷ total cost at the best day, in percent.</summary>
    public decimal? RoiAtBestDayPercent { get; init; }

    /// <summary>Weight, total cost and profit on the last day of the projection.</summary>
    public decimal? WeightAtHorizonKg { get; init; }

    /// <inheritdoc cref="WeightAtHorizonKg"/>
    public decimal? CostAtHorizonBdt { get; init; }

    /// <inheritdoc cref="WeightAtHorizonKg"/>
    public decimal? ProfitAtHorizonBdt { get; init; }

    /// <summary>Live price per kg needed to break even on the last day: (total cost − per-head addition) ÷ weight.</summary>
    public decimal? BreakEvenPricePerKgAtHorizonBdt { get; init; }

    /// <summary>What tomorrow's gain is worth at the sale price.</summary>
    public decimal? ValueOfDailyGainBdt { get; init; }

    /// <summary>Tomorrow's gain value − tomorrow's cost: what keeping it one more day earns.</summary>
    public decimal? MarginPerDayKeptBdt { get; init; }

    // ── Health ──
    public int TreatmentsLast90Days { get; init; }
    public decimal HealthCostLast90DaysBdt { get; init; }
    public bool FrequentlySick { get; init; }
    public int BatchDeathsLast90Days { get; init; }

    // ── Verdict ──
    public SellOrKeepVerdict Verdict { get; init; }
    public SellOrKeepLossCause LossCause { get; init; }
    public SellOrKeepConfidence Confidence { get; init; }
    public string ReasonEn { get; init; } = string.Empty;
    public string ReasonBn { get; init; } = string.Empty;

    /// <summary>Day-by-day projection (day 0 = today). Empty for herd lists.</summary>
    public IReadOnlyList<SellOrKeepPoint> Projection { get; init; } = [];
}

/// <summary>
/// The Fattening plan: the animal's own figures (<see cref="Defaults"/>, the same advice as the
/// "Sell or keep?" card, without its projection) and the projection with the what-if
/// <see cref="Overrides"/> applied (<see cref="Plan"/>). With no overrides the two agree on every figure.
/// </summary>
public sealed record FatteningPlan(SellOrKeepAdvice Defaults, SellOrKeepAdvice Plan, SellOrKeepOverrides Overrides);
