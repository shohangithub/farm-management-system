namespace Farm360.Application.Intelligence.SellOrKeep;

/// <summary>
/// Every assumption behind the "Sell or keep?" advice, in one place. The report notes, the API
/// response and the unit tests all read these, so changing a number here changes it everywhere.
/// </summary>
public sealed record SellOrKeepAssumptions
{
    /// <summary>The defaults the app uses.</summary>
    public static SellOrKeepAssumptions Default { get; } = new();

    /// <summary>How many days ahead the projection looks (day 0 = today).</summary>
    public int HorizonDays { get; init; } = 240;

    /// <summary>
    /// Recent growth = the least-squares slope of the weighings in this many days before today
    /// (at least two weighings); otherwise the lifetime gain per day (last − first weighing).
    /// </summary>
    public int RecentGrowthWindowDays { get; init; } = 60;

    /// <summary>
    /// Growth slows as an animal gets heavier. The projected daily gain falls in a straight line by
    /// this fraction of today's gain over <see cref="GrowthDeclineDays"/> days, then stays at that floor
    /// (0.30 over 180 days: a 1.0 kg/day animal gains 0.85 kg/day after 90 days and 0.70 kg/day from day 180 on).
    /// </summary>
    public decimal GrowthDeclineFraction { get; init; } = 0.30m;

    /// <summary>Days over which <see cref="GrowthDeclineFraction"/> is reached.</summary>
    public int GrowthDeclineDays { get; init; } = 180;

    /// <summary>Feed cost per day is the average of the feed actually eaten in this many days before today.</summary>
    public int FeedWindowDays { get; init; } = 30;

    /// <summary>Labour, overhead and health cost per day, and the sickness count, use this many days before today.</summary>
    public int HealthWindowDays { get; init; } = 90;

    /// <summary>
    /// A heavier animal eats more: future feed cost per day = today's feed cost × (weight ÷ today's weight)^0.75
    /// (metabolic body weight).
    /// </summary>
    public double FeedScalingExponent { get; init; } = 0.75;

    /// <summary>When the most profitable day is this close (or closer), the advice is "Sell now".</summary>
    public int SellNowWithinDays { get; init; } = 7;

    /// <summary>The farm's own sales of the same breed count when sold within this many days.</summary>
    public int OwnSalesLookbackDays { get; init; } = 365;

    /// <summary>Own sales of the breed needed before their average price per kg is used.</summary>
    public int MinOwnSales { get; init; } = 2;

    /// <summary>Treatments in <see cref="HealthWindowDays"/> that mark an animal as frequently sick.</summary>
    public int FrequentlySickTreatments { get; init; } = 3;

    /// <summary>Health cost per day above this share of feed cost per day also marks it as frequently sick.</summary>
    public decimal FrequentlySickHealthShareOfFeed { get; init; } = 0.25m;

    /// <summary>Recent growth within ± this share of the breed's target counts as "at target".</summary>
    public decimal AtTargetBand { get; init; } = 0.10m;

    /// <summary>A latest weighing older than this lowers the confidence one step.</summary>
    public int StaleWeighingDays { get; init; } = 45;

    /// <summary>Today's weight is the latest weighing plus growth since then, for at most this many days.</summary>
    public int MaxExtrapolationDays { get; init; } = 60;

    /// <summary>Fit quality (R²) needed for high confidence with 4+ recent weighings.</summary>
    public decimal HighConfidenceFit { get; init; } = 0.85m;

    /// <summary>Fit quality (R²) needed for medium confidence with 3 recent weighings.</summary>
    public decimal MediumConfidenceFit { get; init; } = 0.60m;

    /// <summary>The single-animal report prints the projection every this many days.</summary>
    public int ProjectionTableStepDays { get; init; } = 15;
}
