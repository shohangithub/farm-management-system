using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One animal's lifetime cost performance, for ranking against the herd-mates present in the period.</summary>
public sealed record HerdPerformanceReportRow(
    string Tag,
    string Status,
    decimal CurrentWeightKg,
    decimal AdgKg,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal TotalCostBdt,
    decimal CostPerKgBdt,
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

    public override LocalizedText Title => new("Herd Growth & Performance Ranking", "পালের বৃদ্ধি ও কর্মক্ষমতা র‍্যাঙ্কিং");

    public override LocalizedText Description => new(
        "Every animal present in the period, ranked by lifetime cost per kg of live weight, with lifetime ADG, feed and full-absorption total cost.",
        "নির্বাচিত সময়ে উপস্থিত সকল পশুর আজীবন প্রতি জীবন্ত কেজি খরচ অনুসারে ক্রম, আজীবন দৈনিক বৃদ্ধি, খাদ্য ও মোট খরচসহ।");

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
        ReportColumn<HerdPerformanceReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.Tag, 28f, false),
        ReportColumn<HerdPerformanceReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, 24f, false),
        ReportColumn<HerdPerformanceReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.CurrentWeightKg, 1, 24f),
        // Every figure below is lifetime (acquisition to today); the period only chooses which
        // animals appear. Mixing period feed with a lifetime total let a row show feed cost that
        // was not the feed inside its own total cost.
        ReportColumn<HerdPerformanceReportRow>.Number("adg", new LocalizedText("Lifetime ADG (kg/d)", "আজীবন দৈনিক বৃদ্ধি"), r => r.AdgKg, 3, 22f),
        ReportColumn<HerdPerformanceReportRow>.Number("feedKg", new LocalizedText("Lifetime feed (kg)", "আজীবন খাদ্য (কেজি)"), r => r.FeedKg, 2, 24f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("feedCost", new LocalizedText("Lifetime feed cost", "আজীবন খাদ্য খরচ"), r => r.FeedCostBdt, 2, 26f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("totalCost", new LocalizedText("Lifetime total cost", "আজীবন মোট খরচ"), r => r.TotalCostBdt, 2, 28f, ReportAggregate.Sum),
        ReportColumn<HerdPerformanceReportRow>.Money("costPerKg", new LocalizedText("Lifetime cost / live kg", "আজীবন খরচ / জীবন্ত কেজি"), r => r.CostPerKgBdt, 2, 24f, ReportAggregate.None),
        ReportColumn<HerdPerformanceReportRow>.Text("band", new LocalizedText("Band", "স্তর"), r => r.Band, 22f, false),
    ];

    public override ReportGroup<HerdPerformanceReportRow>? Group =>
        ReportGroup<HerdPerformanceReportRow>.By(r => r.Band, r => r.Band);

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

            var weight = animal.LatestWeightKg ?? 0m;

            // The feed cost shown is the feed inside the total: both come from the cost ledger
            // (whose feed bucket mirrors the confirmed allocations), or both from the allocations
            // when the animal has no ledger yet.
            var cost = _valuation.CostToDate(animal, ledger, feed.TotalCostBdt);
            var totalCost = cost.TotalBdt;

            rows.Add(new HerdPerformanceReportRow(
                animal.Tag.TagId,
                animal.Status.ToString(),
                weight,
                animal.AdgKgPerDay ?? 0m,
                feed.TotalKg,
                cost.FeedBdt,
                totalCost,
                weight > 0 ? decimal.Round(totalCost / weight, 2, MidpointRounding.AwayFromZero) : 0m,
                "Unranked"));
        }

        // Worst first: the report exists to find the animals losing money.
        var ranked = rows
            .OrderByDescending(r => r.CostPerKgBdt)
            .ThenBy(r => r.Tag, StringComparer.Ordinal)
            .ToList();

        return ApplyQuartileBands(ranked);
    }

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
            return ranked.Select(r => r with { Band = "All animals" }).ToList();
        }

        var quartile = (int)Math.Ceiling(ranked.Count / 4.0);
        var banded = new List<HerdPerformanceReportRow>(ranked.Count);

        for (var i = 0; i < ranked.Count; i++)
        {
            var band = i < quartile
                ? "Most expensive quartile"
                : i >= ranked.Count - quartile
                    ? "Most efficient quartile"
                    : "Middle";

            banded.Add(ranked[i] with { Band = band });
        }

        return banded;
    }
}
