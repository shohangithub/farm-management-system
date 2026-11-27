using Farm360.Application.Intelligence.Services;

namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>One weighing.</summary>
public sealed record SellOrKeepWeighing(DateOnly Date, decimal WeightKg);

/// <summary>Where the sale price per kg live weight came from, in priority order.</summary>
public enum SellOrKeepPriceSource
{
    /// <summary>Average of the farm's own sales of the same breed in the last 12 months (at least two).</summary>
    OwnSales = 0,

    /// <summary>The expected price per kg set on the breed.</summary>
    BreedExpected = 1,

    /// <summary>The farm rule's live-weight price (valuation method "live weight market").</summary>
    FarmLiveRate = 2,

    /// <summary>The farm rule's meat price × dressing %.</summary>
    MeatAndDressing = 3,
}

/// <summary>The sale price per kg live weight and where it came from.</summary>
public sealed record SellOrKeepPrice(decimal PricePerKgBdt, SellOrKeepPriceSource Source, int SalesCount = 0);

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

    public required SellOrKeepPrice Price { get; init; }
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
    public SellOrKeepGrowthVsTarget GrowthVsTarget { get; init; }

    // ── Cost run-rate ──
    public decimal FeedPerDayBdt { get; init; }
    public decimal LabourOverheadPerDayBdt { get; init; }
    public decimal HealthPerDayBdt { get; init; }
    public decimal DailyCostBdt => FeedPerDayBdt + LabourOverheadPerDayBdt + HealthPerDayBdt;

    /// <summary>Feed cost per kg gained (feed per day ÷ gain per day), when it grows.</summary>
    public decimal? FeedCostPerKgGainBdt { get; init; }

    // ── Price ──
    public required SellOrKeepPrice Price { get; init; }

    // ── Projection ──
    public int? BestDay { get; init; }
    public DateOnly? BestDate { get; init; }
    public decimal? BestWeightKg { get; init; }
    public decimal? ProfitAtBestDayBdt { get; init; }
    public decimal? ExtraProfitBdt => ProfitAtBestDayBdt - ProfitTodayBdt;
    public int? BreakEvenDay { get; init; }
    public DateOnly? BreakEvenDate { get; init; }

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
