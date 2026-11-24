using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One animal's lifetime cost performance, for ranking against the herd-mates present in the period.</summary>
/// <remarks>
/// <see cref="CostPerKgBdt"/> is null when there is no live kilo to divide by: no weight recorded,
/// or the animal died. Those rows sit in their own bands rather than being ranked as "cheapest".
/// </remarks>
public sealed record HerdPerformanceReportRow(
    string Tag,
    string Status,
    decimal? CurrentWeightKg,
    decimal? AdgKg,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal TotalCostBdt,
    decimal? CostPerKgBdt,
    string Band);

/// <summary>
/// Report H6 — every animal in a farm ranked by what it costs to put a kilo on it.
/// </summary>
/// <remarks>
/// <para>
/// Ranked by cost per kg of live weight, worst first: the point of the report is to surface the
/// animals losing money, not to congratulate the ones that are not.
/// </para>
/// <para>
/// Depends on both gaps being closed. Feed comes from <c>AnimalFeedAllocation</c> (GAP-1) and the
/// total cost from <c>AnimalCostLedger</c> now that labour and overhead are posted to it (GAP-2).
/// Ranking on the old direct-cost-only total would have flattered every animal by the same
/// absolute amount and, because that amount is spread by head-days rather than by weight,
/// re-ordered the tail of the list.
/// </para>
/// </remarks>
public sealed class HerdPerformanceReportDefinition : ReportDefinition<HerdPerformanceReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalValuationService _valuation;

    public HerdPerformanceReportDefinition(
        IAnimalRepository animals,
        IAnimalFeedAllocationRepository feed,
        IAnimalCostLedgerRepository ledgers,
        IAnimalValuationService valuation)
    {
        _animals = animals;
        _feed = feed;
        _ledgers = ledgers;
        _valuation = valuation;
    }

    public override string Key => "livestock.herd-performance";

    public override LocalizedText Title => new("Cost per kg — Herd Ranking", "প্রতি কেজি খরচ — পালের ক্রম");

    public override LocalizedText Description => new(
        "Which animals cost the most to put weight on? Every animal on the farm in the period, ranked by its total cost so far per kg of live weight, most expensive first.",
        "কোন পশুর ওজন বাড়াতে সবচেয়ে বেশি খরচ হচ্ছে? নির্বাচিত সময়ে খামারে থাকা প্রতিটি পশু, এ পর্যন্ত মোট খরচ ÷ জীবন্ত ওজন অনুযায়ী সাজানো, সবচেয়ে ব্যয়বহুল আগে।");

    public override ReportCategory Category => ReportCategory.Livestock;

    // Nine columns of numbers read better across the page than down it.
    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(@default: "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<HerdPerformanceReportRow>> Columns =>
    [
        ReportColumn<HerdPerformanceReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.Tag, 2.2f),
        ReportColumn<HerdPerformanceReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, 24f, false),
        ReportColumn<HerdPerformanceReportRow>.Number("weight", new LocalizedText("Live weight (kg)", "জীবন্ত ওজন (কেজি)"), r => r.CurrentWeightKg, 1, 24f),
        // Every figure below is lifetime (acquisition to today, or to the exit date); the period
        // only chooses which animals appear. Mixing period feed with a lifetime total let a row
        // show feed cost that was not the feed inside its own total cost.
        ReportColumn<HerdPerformanceReportRow>.Number("adg", new LocalizedText("Daily gain (kg/day)", "দৈনিক বৃদ্ধি (কেজি/দিন)"), r => r.AdgKg, 3, 24f),
        ReportColumn<HerdPerformanceReportRow>.Number("feedKg", new LocalizedText("Feed eaten (kg)", "খাওয়া খাদ্য (কেজি)"), r => r.FeedKg, 1, 24f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("feedCost", new LocalizedText("Feed cost (BDT)", "খাদ্য খরচ (টাকা)"), r => r.FeedCostBdt, 2, 28f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("totalCost", new LocalizedText("Total cost so far (BDT)", "এ পর্যন্ত মোট খরচ (টাকা)"), r => r.TotalCostBdt, 2, 30f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("costPerKg", new LocalizedText("Cost per live kg (BDT/kg)", "প্রতি জীবন্ত কেজিতে খরচ (টাকা/কেজি)"), r => r.CostPerKgBdt, 2, 30f, ReportAggregate.None),
    ];

    public override ReportGroup<HerdPerformanceReportRow>? Group =>
        ReportGroup<HerdPerformanceReportRow>.ByLocalized(r => r.Band, r => BandLabel(r.Band));

    public override LocalizedText? GrandTotalLabel => new("All animals", "সকল পশু");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one animal that was on the farm at some point in the period; all figures are for its whole time on the farm.",
            "প্রতিটি সারি নির্বাচিত সময়ে খামারে থাকা একটি পশু; সব হিসাব খামারে তার পুরো সময়ের।"),
        new("Cost per live kg = total cost so far (purchase + feed + vet + labour + overhead) ÷ live weight; for a sold animal the sale weight is used when recorded.",
            "প্রতি জীবন্ত কেজিতে খরচ = এ পর্যন্ত মোট খরচ (কেনা + খাদ্য + চিকিৎসা + শ্রম + অন্যান্য) ÷ জীবন্ত ওজন; বিক্রি হওয়া পশুর ক্ষেত্রে বিক্রির ওজন থাকলে সেটি ধরা হয়েছে।"),
        new("Animals are split into the most expensive quarter, the middle and the cheapest quarter, so the costly ones stand out.",
            "পশুগুলোকে সবচেয়ে ব্যয়বহুল এক-চতুর্থাংশ, মাঝের অংশ ও সবচেয়ে সাশ্রয়ী এক-চতুর্থাংশে ভাগ করা হয়েছে, যাতে বেশি খরচের পশু সহজে চোখে পড়ে।"),
        new("Animals that died are a total loss and are listed separately, not ranked; animals with no weight recorded are also listed separately.",
            "মৃত পশু সম্পূর্ণ ক্ষতি, তাই আলাদা দেখানো হয়েছে, ক্রমে নেই; যাদের ওজন রেকর্ড নেই তারাও আলাদা।"),
        new("Feed and cost stop at the sale or death date.",
            "বিক্রি বা মৃত্যুর তারিখে খাদ্য ও খরচের হিসাব থেমে যায়।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = context.Range("period");
        var range = $"{period.From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {period.To.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"{range} সময়ে খামারে থাকা পশু"
            : $"Animals on the farm {range}");
    }

    protected override async Task<IReadOnlyList<HerdPerformanceReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var period = context.Range("period");

        var herd = await _animals
            .GetPresentDuringPeriodAsync(farmId, period.From, period.To, cancellationToken)
            .ConfigureAwait(false);

        if (herd.Count == 0)
        {
            return [];
        }

        // Lifetime feed for the whole farm in one grouped query, and every ledger in one more,
        // rather than one round trip per animal.
        var feedTotals = (await _feed
                .GetTotalsForFarmAsync(farmId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken)
                .ConfigureAwait(false))
            .ToDictionary(t => t.AnimalId);

        var ledgers = (await _ledgers.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false))
            .GroupBy(l => l.AnimalId)
            .ToDictionary(g => g.Key, g => g.First());

        var rows = new List<HerdPerformanceReportRow>(herd.Count);

        foreach (var animal in herd)
        {
            ledgers.TryGetValue(animal.Id, out var ledger);
            var feed = feedTotals.TryGetValue(animal.Id, out var f) ? f : default;

            var died = animal.Status == AnimalStatus.Dead;

            // A sold animal's live kilos are the ones weighed at sale, when recorded.
            var weight = animal.Status is AnimalStatus.Sold or AnimalStatus.Slaughtered && animal.SaleWeightKg is > 0m
                ? animal.SaleWeightKg
                : animal.LatestWeightKg;

            // The feed cost shown is the feed inside the total: both come from the cost ledger
            // (whose feed bucket mirrors the confirmed allocations), or both from the allocations
            // when the animal has no ledger yet.
            var cost = _valuation.CostToDate(animal, ledger, feed.TotalCostBdt);
            var totalCost = cost.TotalBdt;

            // Fix: an animal with no weight used to get cost/kg 0 and be ranked "most efficient",
            // and a dead animal was ranked on kilos that will never be sold. Neither is ranked now.
            decimal? costPerKg = !died && weight is > 0m
                ? decimal.Round(totalCost / weight.Value, 2, MidpointRounding.AwayFromZero)
                : null;

            rows.Add(new HerdPerformanceReportRow(
                AnimalLabels.Tag(animal),
                StatusLabel(animal.Status, context.Language == ReportLanguage.Bn),
                weight,
                animal.AdgKgPerDay,
                feed.TotalKg,
                cost.FeedBdt,
                totalCost,
                costPerKg,
                died ? BandDied : costPerKg is null ? BandNoWeight : BandUnranked));
        }

        // Worst first: the report exists to find the animals losing money.
        var ranked = rows
            .Where(r => r.Band == BandUnranked)
            .OrderByDescending(r => r.CostPerKgBdt)
            .ThenBy(r => r.Tag, StringComparer.Ordinal)
            .ToList();

        var result = ApplyQuartileBands(ranked);
        result.AddRange(rows.Where(r => r.Band == BandNoWeight).OrderBy(r => r.Tag, StringComparer.Ordinal));
        result.AddRange(rows.Where(r => r.Band == BandDied).OrderByDescending(r => r.TotalCostBdt).ThenBy(r => r.Tag, StringComparer.Ordinal));
        return result;
    }

    private const string BandUnranked = "unranked";
    private const string BandAll = "all";
    private const string BandExpensive = "1-expensive";
    private const string BandMiddle = "2-middle";
    private const string BandEfficient = "3-efficient";
    private const string BandNoWeight = "4-no-weight";
    private const string BandDied = "5-died";

    private static LocalizedText BandLabel(string band) => band switch
    {
        BandAll => new("All weighed animals (too few to split into quarters)", "সকল ওজনকৃত পশু (ভাগ করার মতো যথেষ্ট নয়)"),
        BandExpensive => new("Most expensive quarter", "সবচেয়ে ব্যয়বহুল এক-চতুর্থাংশ"),
        BandMiddle => new("Middle", "মাঝের অংশ"),
        BandEfficient => new("Cheapest quarter", "সবচেয়ে সাশ্রয়ী এক-চতুর্থাংশ"),
        BandNoWeight => new("No weight recorded — not ranked", "ওজন রেকর্ড নেই — ক্রমে নেই"),
        BandDied => new("Died — total loss", "মৃত — সম্পূর্ণ ক্ষতি"),
        _ => new(band, band),
    };

    private static string StatusLabel(AnimalStatus status, bool bn) => status switch
    {
        AnimalStatus.Active => bn ? "খামারে" : "On farm",
        AnimalStatus.Quarantined => bn ? "আলাদা রাখা" : "Quarantined",
        AnimalStatus.Sold => bn ? "বিক্রি" : "Sold",
        AnimalStatus.Slaughtered => bn ? "জবাই" : "Slaughtered",
        AnimalStatus.Dead => bn ? "মৃত" : "Died",
        AnimalStatus.Transferred => bn ? "স্থানান্তরিত" : "Transferred",
        _ => status.ToString(),
    };

    /// <summary>
    /// Labels each animal by quartile so the eye finds the tail without reading every row.
    /// </summary>
    /// <remarks>
    /// Relative, not absolute: a "good" cost per kg depends on breed, season and feed price, so a
    /// fixed threshold would be wrong somewhere. Quartiles always answer the question the farm
    /// manager is actually asking — which of my animals are the expensive ones.
    /// </remarks>
    private static List<HerdPerformanceReportRow> ApplyQuartileBands(List<HerdPerformanceReportRow> ranked)
    {
        // Bands need a spread to be meaningful; under four head, say so rather than imply one.
        if (ranked.Count < 4)
        {
            return ranked.Select(r => r with { Band = BandAll }).ToList();
        }

        var quartile = (int)Math.Ceiling(ranked.Count / 4.0);
        var banded = new List<HerdPerformanceReportRow>(ranked.Count);

        for (var i = 0; i < ranked.Count; i++)
        {
            var band = i < quartile
                ? BandExpensive
                : i >= ranked.Count - quartile
                    ? BandEfficient
                    : BandMiddle;

            banded.Add(ranked[i] with { Band = band });
        }

        return banded;
    }
}
