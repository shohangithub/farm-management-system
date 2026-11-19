using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Health;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One group's growth and feed efficiency over a period. Null means "not measurable", never zero.</summary>
public sealed record BatchPerformanceMetrics(
    int HeadStart,
    int HeadEnd,
    decimal AverageHead,
    long HeadDays,
    int AnimalsPresent,
    int AnimalsWeighed,
    decimal? WeightGainKg,
    decimal? AdgKgPerDay,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal? Fcr,
    decimal? FeedCostPerKgGainBdt,
    decimal? FeedCostPerHeadDayBdt)
{
    public int AnimalsMissingWeights => AnimalsPresent - AnimalsWeighed;
}

/// <summary>
/// The growth and feed-conversion arithmetic, pure so it is tested rather than described.
/// </summary>
/// <remarks>
/// <para>
/// Gain is measured, never estimated: an animal counts towards gain only when it was weighed at
/// least twice, on different days, inside the period; its gain is last − first weigh-in. Animals
/// without that are reported as missing weights, not given an assumed gain.
/// </para>
/// <para>
/// FCR = feed eaten ÷ weight gained, over the <i>same</i> days: for each weighed animal, the
/// confirmed feed from its first weigh-in up to (not including) its last. Dividing a whole period's
/// feed by a gain measured over part of it would overstate FCR. The feed column itself is the
/// whole period's confirmed feed for every animal present.
/// </para>
/// </remarks>
public static class BatchPerformanceCalculator
{
    public static BatchPerformanceMetrics Compute(
        IReadOnlyDictionary<Guid, HerdMetrics.Residency> animals,
        IEnumerable<HerdMetrics.WeighIn> weighIns,
        IEnumerable<AnimalFeedDay> feedDays,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(animals);
        ArgumentNullException.ThrowIfNull(weighIns);
        ArgumentNullException.ThrowIfNull(feedDays);

        static bool OnFarm(HerdMetrics.Residency r, DateOnly day) => r.Entry <= day && (r.Exit is null || day < r.Exit);

        var days = to.DayNumber - from.DayNumber + 1;
        long headDays = 0;
        var present = new HashSet<Guid>();
        foreach (var (id, r) in animals)
        {
            var start = Math.Max(r.Entry.DayNumber, from.DayNumber);
            var endExclusive = Math.Min(r.Exit?.DayNumber ?? int.MaxValue, to.DayNumber + 1);
            if (endExclusive > start)
            {
                headDays += endExclusive - start;
                present.Add(id);
            }
        }

        var weights = weighIns
            .Where(w => present.Contains(w.AnimalId) && w.Date >= from && w.Date <= to)
            .ToList();
        var feed = feedDays
            .Where(f => present.Contains(f.AnimalId) && f.Date >= from && f.Date <= to)
            .ToList();
        var feedByAnimal = feed.ToLookup(f => f.AnimalId);

        decimal gain = 0m, spanFeedKg = 0m, spanFeedCost = 0m;
        var weighed = 0;
        foreach (var g in weights.GroupBy(w => w.AnimalId))
        {
            var ordered = g.OrderBy(w => w.Date).ToList();
            var first = ordered[0];
            var last = ordered[^1];
            if (last.Date <= first.Date)
            {
                continue;
            }

            weighed++;
            gain += last.WeightKg - first.WeightKg;
            foreach (var f in feedByAnimal[g.Key].Where(f => f.Date >= first.Date && f.Date < last.Date))
            {
                spanFeedKg += f.Kg;
                spanFeedCost += f.CostBdt;
            }
        }

        var feedKg = feed.Sum(f => f.Kg);
        var feedCost = feed.Sum(f => f.CostBdt);
        var adg = HerdMetrics.AverageDailyGain(weights, from, to);
        decimal? measuredGain = weighed > 0 ? gain : null;

        return new BatchPerformanceMetrics(
            animals.Values.Count(r => OnFarm(r, from)),
            animals.Values.Count(r => OnFarm(r, to)),
            days > 0 ? decimal.Round((decimal)headDays / days, 2) : 0m,
            headDays,
            present.Count,
            weighed,
            measuredGain,
            adg is { } a ? (decimal)a : null,
            feedKg,
            feedCost,
            gain > 0m && spanFeedKg > 0m ? decimal.Round(spanFeedKg / gain, 2) : null,
            gain > 0m && spanFeedCost > 0m ? decimal.Round(spanFeedCost / gain, 2) : null,
            headDays > 0 ? decimal.Round(feedCost / headDays, 2) : null);
    }
}

public sealed record BatchPerformanceReportRow(
    string Batch,
    int HeadStart,
    int HeadEnd,
    decimal AverageHead,
    string Weighed,
    decimal? WeightGainKg,
    decimal? AdgKgPerDay,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal? Fcr,
    decimal? FeedCostPerKgGainBdt,
    decimal? FeedCostPerHeadDayBdt,
    string Flags);

/// <summary>
/// Report L7 — batch performance &amp; FCR: per batch, head count at the start and end of the period,
/// measured weight gain and ADG, confirmed feed (kg and cost), feed conversion ratio, feed cost per
/// kg gained and per head-day — with animals lacking weigh-ins flagged rather than estimated.
/// </summary>
/// <remarks>
/// Feed is the confirmed per-animal allocation (<c>AnimalFeedAllocation</c>), the same source as the
/// feeding and cost-ledger reports. Batch is each animal's batch as recorded now — batch membership
/// has no history — and animals in no batch are shown on their own line.
/// </remarks>
public sealed class BatchPerformanceReportDefinition : ReportDefinition<BatchPerformanceReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IAnimalBatchRepository _batches;
    private readonly IAnimalFeedAllocationRepository _feed;

    public BatchPerformanceReportDefinition(
        IAnimalRepository animals,
        IAnimalBatchRepository batches,
        IAnimalFeedAllocationRepository feed)
    {
        _animals = animals;
        _batches = batches;
        _feed = feed;
    }

    public override string Key => "livestock.batch-performance";

    public override LocalizedText Title => new("Batch Performance & FCR", "ব্যাচ কর্মক্ষমতা ও খাদ্য রূপান্তর অনুপাত (FCR)");

    public override LocalizedText Description => new(
        "Per batch: head count start/end, measured weight gain and ADG, confirmed feed kg and cost, FCR, feed cost per kg gain and per head-day; missing weigh-ins flagged.",
        "ব্যাচভিত্তিক: শুরু/শেষের পশুসংখ্যা, পরিমাপকৃত ওজন বৃদ্ধি ও দৈনিক বৃদ্ধি, নিশ্চিত খাদ্য (কেজি ও খরচ), FCR, প্রতি কেজি বৃদ্ধিতে ও প্রতি পশু-দিনে খাদ্য খরচ; ওজন না থাকলে চিহ্নিত।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override LocalizedText? GrandTotalLabel => new("Farm total", "খামারের মোট");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "last-90-days"),
        ReportParameter.Batch("batchId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<BatchPerformanceReportRow>> Columns =>
    [
        ReportColumn<BatchPerformanceReportRow>.Text("batch", new LocalizedText("Batch", "ব্যাচ"), r => r.Batch, width: 2f),
        ReportColumn<BatchPerformanceReportRow>.WholeNumber("headStart", new LocalizedText("Head at start", "শুরুতে পশু"), r => r.HeadStart, 16f, ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.WholeNumber("headEnd", new LocalizedText("Head at end", "শেষে পশু"), r => r.HeadEnd, 16f, ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.Number("avgHead", new LocalizedText("Avg head", "গড় পশু"), r => r.AverageHead, 1, 16f, ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.Text("weighed", new LocalizedText("Weighed / present", "ওজনকৃত / উপস্থিত"), r => r.Weighed, width: 20f, relative: false, align: ReportAlign.Right),
        ReportColumn<BatchPerformanceReportRow>.Number("gain", new LocalizedText("Weight gain (kg)", "ওজন বৃদ্ধি (কেজি)"), r => r.WeightGainKg, 1, 22f, ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.Number("adg", new LocalizedText("ADG (kg/day)", "দৈনিক বৃদ্ধি (কেজি)"), r => r.AdgKgPerDay, 3, 18f),
        ReportColumn<BatchPerformanceReportRow>.Number("feedKg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.FeedKg, 1, 22f, ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.Money("feedCost", new LocalizedText("Feed cost (BDT)", "খাদ্য খরচ (টাকা)"), r => r.FeedCostBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<BatchPerformanceReportRow>.Number("fcr", new LocalizedText("FCR (kg feed / kg gain)", "FCR (খাদ্য/বৃদ্ধি)"), r => r.Fcr, 2, 18f),
        ReportColumn<BatchPerformanceReportRow>.Money("costPerKgGain", new LocalizedText("Feed cost / kg gain", "প্রতি কেজি বৃদ্ধিতে খাদ্য খরচ"), r => r.FeedCostPerKgGainBdt, widthMm: 22f),
        ReportColumn<BatchPerformanceReportRow>.Money("costPerHeadDay", new LocalizedText("Feed cost / head-day", "প্রতি পশু-দিনে খাদ্য খরচ"), r => r.FeedCostPerHeadDayBdt, widthMm: 22f),
        ReportColumn<BatchPerformanceReportRow>.Text("flags", new LocalizedText("Data notes", "তথ্য সংক্রান্ত মন্তব্য"), r => r.Flags, width: 2.4f),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var farm = data.Farm;
        string N(decimal v, int d) => ReportValueFormatter.Format(v, d == 0 ? ReportColumnType.WholeNumber : ReportColumnType.Number, d, context.BengaliNumerals);
        var fcr = farm.Fcr is { } f ? N(f, 2) : "—";

        return context.Language == ReportLanguage.Bn
            ? $"খামারের FCR {fcr} ({N(farm.AnimalsWeighed, 0)} টি ওজনকৃত পশুর ভিত্তিতে) · {N(farm.AnimalsMissingWeights, 0)} টি পশুর পর্যাপ্ত ওজন নেই"
            : $"Farm FCR {fcr} (over {N(farm.AnimalsWeighed, 0)} weighed animals) · {N(farm.AnimalsMissingWeights, 0)} animals lack two weigh-ins in the period";
    }

    protected override async Task<IReadOnlyList<BatchPerformanceReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Rows;
    }

    private sealed record PerformanceData(IReadOnlyList<BatchPerformanceReportRow> Rows, BatchPerformanceMetrics Farm);

    private Task<PerformanceData> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("batch-performance", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var batchFilter = context.IdOrNull("batchId");
            var bn = context.Language == ReportLanguage.Bn;

            var animals = (await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false))
                .Where(a => batchFilter is null || a.BatchId == batchFilter)
                .ToList();
            var weighIns = (await _animals.GetWeighInsByFarmAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false))
                .Select(w => new HerdMetrics.WeighIn(w.AnimalId, w.Date, w.WeightKg))
                .ToList();
            var feed = await _feed.GetDailyEatenByFarmAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);
            var batches = (await _batches.GetPagedAsync(1, 10_000, farmId, null, cancellationToken).ConfigureAwait(false)).Items
                .ToDictionary(b => b.Id);

            var residency = animals.ToDictionary(a => a.Id, MortalityMath.Residency);
            var noBatch = bn ? "ব্যাচবিহীন পশু" : "Animals in no batch";

            var rows = new List<BatchPerformanceReportRow>();
            foreach (var group in animals
                         .GroupBy(a => a.BatchId is { } id && batches.ContainsKey(id) ? id : Guid.Empty)
                         .OrderBy(g => g.Key == Guid.Empty ? 1 : 0)
                         .ThenBy(g => g.Key == Guid.Empty ? string.Empty : batches[g.Key].Name, StringComparer.OrdinalIgnoreCase))
            {
                var members = group.ToDictionary(a => a.Id, a => residency[a.Id]);
                var m = BatchPerformanceCalculator.Compute(members, weighIns, feed, period.From, period.To);
                if (m.AnimalsPresent == 0)
                {
                    continue;
                }

                var name = group.Key == Guid.Empty ? noBatch : batches[group.Key].Name;
                rows.Add(ToRow(name, m, bn));
            }

            var farm = BatchPerformanceCalculator.Compute(residency, weighIns, feed, period.From, period.To);
            return new PerformanceData(rows, farm);
        });

    /// <summary>Formats one group's metrics, spelling out what could not be measured and why.</summary>
    public static BatchPerformanceReportRow ToRow(string name, BatchPerformanceMetrics m, bool bn)
    {
        ArgumentNullException.ThrowIfNull(m);
        var flags = new List<string>(3);

        if (m.AnimalsMissingWeights > 0)
        {
            flags.Add(bn
                ? $"{m.AnimalsMissingWeights} টি পশুর এই সময়ে দুইবার ওজন নেই — বৃদ্ধি ও FCR থেকে বাদ"
                : $"{m.AnimalsMissingWeights} of {m.AnimalsPresent} not weighed twice — left out of gain and FCR");
        }

        if (m.WeightGainKg is { } g && g <= 0m)
        {
            flags.Add(bn ? "কোনো ধনাত্মক ওজন বৃদ্ধি মাপা যায়নি — FCR নির্ণয় হয়নি" : "No positive gain measured — FCR not calculated");
        }

        if (m.FeedKg == 0m)
        {
            flags.Add(bn ? "এই সময়ে নিশ্চিত খাদ্য নেই" : "No confirmed feed in the period");
        }

        return new BatchPerformanceReportRow(
            name,
            m.HeadStart,
            m.HeadEnd,
            m.AverageHead,
            $"{m.AnimalsWeighed} / {m.AnimalsPresent}",
            m.WeightGainKg,
            m.AdgKgPerDay,
            m.FeedKg,
            m.FeedCostBdt,
            m.Fcr,
            m.FeedCostPerKgGainBdt,
            m.FeedCostPerHeadDayBdt,
            flags.Count == 0 ? "—" : string.Join("; ", flags));
    }
}
