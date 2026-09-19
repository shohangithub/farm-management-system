namespace Farm360.Domain.BusinessRules;

/// <summary>How a group feed quantity is divided between the animals sharing it.</summary>
public enum FeedAllocationMethod
{
    /// <summary>Every head gets an equal share. Simple to explain to an auditor.</summary>
    EqualPerHead = 0,

    /// <summary>
    /// Share proportional to metabolic body weight (W^exponent). The default: a 400 kg bull
    /// genuinely eats more than a 150 kg calf, and an equal split makes the calf look
    /// unprofitable while flattering the bull.
    /// </summary>
    MetabolicWeight = 1,

    /// <summary>Share proportional to plain live weight (W^1). Cruder than metabolic weight.</summary>
    LiveWeight = 2,
}

/// <summary>
/// What the quantity on a group feeding plan's rule line means.
/// </summary>
/// <remarks>
/// Farm360's rule lines are matched on a per-animal weight band ("200-250 kg → 3 kg/day") and the
/// WeightPercentage plan type computes <c>bodyWeight × %</c>, so the figure is a per-head ration.
/// A plan covering a batch therefore describes one animal's ration, not the batch's total.
/// The alternative is offered for farms that record group totals instead.
/// </remarks>
public enum GroupPlanQuantityBasis
{
    /// <summary>The rule line is one animal's ration; group total = ration × head count.</summary>
    PerHead = 0,

    /// <summary>The rule line is already the whole group's quantity; it is divided, not multiplied.</summary>
    WholeGroup = 1,
}

/// <summary>How indirect costs are pushed down onto individual animals.</summary>
public enum OverheadAllocationMethod
{
    /// <summary>Total ÷ head-days in the period. The default.</summary>
    PerHeadDay = 0,

    /// <summary>Weighted by each animal's share of total live weight.</summary>
    PerLiveWeight = 1,
}

/// <summary>What period a farm-level indirect cost is treated as covering.</summary>
public enum OverheadCostPeriod
{
    /// <summary>The calendar month of the transaction date. Suits salaries and utility bills.</summary>
    CalendarMonth = 0,

    /// <summary>The transaction date alone. Suits one-off costs such as a single transport run.</summary>
    TransactionDate = 1,
}

/// <summary>How an animal's current value is established.</summary>
public enum AnimalValuationPolicy
{
    /// <summary>What has been spent on it: acquisition + accumulated costs. Conservative.</summary>
    CostBasis = 0,

    /// <summary>Live weight × live-animal market rate.</summary>
    LiveWeightMarket = 1,

    /// <summary>
    /// Live weight × dressing percentage × meat price. The default, because cattle here are
    /// priced on expected meat yield, and it matches the projection engine in docs/31.
    /// </summary>
    MeatYieldMarket = 2,
}

public enum ReportLanguagePreference
{
    English = 0,
    Bangla = 1,
}

/// <summary>QuestPDF licence tier declared for this deployment.</summary>
public enum PdfLicenceMode
{
    /// <summary>Free below USD 1M annual revenue. See <see cref="FarmBusinessRules.PdfLicence"/>.</summary>
    Community = 0,
    Professional = 1,
    Enterprise = 2,
}

/// <summary>
/// The farm's configurable business assumptions, in one place.
/// </summary>
/// <remarks>
/// <para>
/// Every value here is a judgement the business owns, not a technical constant: how shared feed is
/// split, how overhead lands on an animal, what an animal is "worth", which language a report
/// prints in. They are gathered into one bound options object so the assumptions can be changed
/// from configuration without touching the reporting, feeding or costing code.
/// </para>
/// <para>
/// Bound from the <c>Farm360:BusinessRules</c> configuration section. Defaults encode the
/// decisions recorded in docs/32 §9 and docs/33.
/// </para>
/// <para>
/// Changing a rule changes future calculations only. Feed allocations are persisted with the
/// method that produced them, so historical reports keep reprinting the numbers they were signed
/// off with — a rule change must never silently rewrite last quarter's accounts.
/// </para>
/// </remarks>
public sealed class FarmBusinessRules
{
    /// <summary>Configuration section these rules bind from.</summary>
    public const string SectionName = "Farm360:BusinessRules";

    // ── Feed allocation (docs/32 GAP-1) ─────────────────────────────────────

    public FeedAllocationMethod FeedAllocation { get; set; } = FeedAllocationMethod.MetabolicWeight;

    /// <summary>
    /// Exponent for <see cref="FeedAllocationMethod.MetabolicWeight"/>. 0.75 is the standard
    /// metabolic-weight exponent used in ruminant nutrition.
    /// </summary>
    public double MetabolicWeightExponent { get; set; } = 0.75d;

    public GroupPlanQuantityBasis GroupPlanQuantityBasis { get; set; } = GroupPlanQuantityBasis.PerHead;

    /// <summary>
    /// Weight assumed for an animal with no recorded weight, so one unweighed head cannot take a
    /// zero share and silently push its feed cost onto its pen-mates.
    /// </summary>
    public decimal FallbackAnimalWeightKg { get; set; } = 150m;

    // ── Overhead and labour (docs/32 GAP-2) ─────────────────────────────────

    public OverheadAllocationMethod OverheadAllocation { get; set; } = OverheadAllocationMethod.PerHeadDay;

    /// <summary>
    /// The period a farm-level expense is treated as covering.
    /// </summary>
    /// <remarks>
    /// A monthly wage bill is posted on one date but earned across the month. Spreading it over
    /// the calendar month means an animal sold on the 10th carries ten days of labour rather than
    /// all of it or none of it, purely according to which day the bookkeeper posted the entry.
    /// </remarks>
    public OverheadCostPeriod OverheadCostPeriod { get; set; } = OverheadCostPeriod.CalendarMonth;

    // ── Valuation (docs/32 GAP-3) ───────────────────────────────────────────

    public AnimalValuationPolicy Valuation { get; set; } = AnimalValuationPolicy.MeatYieldMarket;

    /// <summary>Carcass yield as a fraction of live weight, when the breed does not state one.</summary>
    public decimal DefaultDressingPercentage { get; set; } = 0.50m;

    /// <summary>Fallback meat price when no farm-level market price is configured.</summary>
    public decimal DefaultMeatPricePerKgBdt { get; set; } = 680m;

    /// <summary>Fallback live-animal rate, used only by <see cref="AnimalValuationPolicy.LiveWeightMarket"/>.</summary>
    public decimal DefaultLiveWeightPricePerKgBdt { get; set; } = 400m;

    // ── Reporting presentation ──────────────────────────────────────────────

    public ReportLanguagePreference DefaultReportLanguage { get; set; } = ReportLanguagePreference.English;

    /// <summary>Print ০-৯ instead of 0-9. Off by default; printed accounts here go both ways.</summary>
    public bool BengaliNumeralsByDefault { get; set; }

    /// <summary>
    /// Declared QuestPDF licence tier.
    /// </summary>
    /// <remarks>
    /// Community is free only while annual revenue is below USD 1M. Nobody but the business can
    /// confirm that figure, so this is configuration rather than a constant: when Farm360 crosses
    /// the threshold, buy the licence and change this value.
    /// </remarks>
    public PdfLicenceMode PdfLicence { get; set; } = PdfLicenceMode.Community;

    /// <summary>Optional licence key, required when <see cref="PdfLicence"/> is not Community.</summary>
    public string? PdfLicenceKey { get; set; }
}
