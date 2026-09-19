using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One weigh-in and what happened in the interval leading up to it.</summary>
public sealed record AnimalGrowthReportRow(
    DateOnly Date,
    decimal WeightKg,
    int IntervalDays,
    decimal GainKg,
    decimal AdgKg,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal Fcr,
    decimal CostPerKgGainBdt);

/// <summary>
/// Report A3 — every weigh-in for one animal, with the feed and cost of each interval between
/// weighings, and the efficiency those two numbers imply.
/// </summary>
/// <remarks>
/// <para>
/// The report that ties inputs to outcome. Weight alone says an animal grew; feed alone says what
/// it ate; only the two together give <b>FCR</b> (kg feed per kg gain) and <b>cost per kg gain</b>,
/// which is what actually decides whether an animal is worth keeping.
/// </para>
/// <para>
/// Depended on GAP-1: the interval feed figures come from <c>AnimalFeedAllocation</c>, so before
/// per-animal allocation existed this report could only have been produced for animals on an
/// individual feeding plan, and would have shown zero feed — and therefore an infinite FCR — for
/// everything else.
/// </para>
/// </remarks>
public sealed class AnimalGrowthReportDefinition : ReportDefinition<AnimalGrowthReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IDailyFeedingEntryRepository _dailyEntries;

    public AnimalGrowthReportDefinition(
        IAnimalRepository animals,
        IAnimalFeedAllocationRepository allocations,
        IDailyFeedingEntryRepository dailyEntries)
    {
        _animals = animals;
        _allocations = allocations;
        _dailyEntries = dailyEntries;
    }

    public override string Key => "livestock.animal-growth";

    public override LocalizedText Title => new("Animal Weight & Growth Report", "পশুর ওজন ও বৃদ্ধি প্রতিবেদন");

    public override LocalizedText Description => new(
        "Weigh-ins with interval gain, ADG, feed consumed, FCR and cost per kg of gain.",
        "ওজন রেকর্ড, বৃদ্ধি, দৈনিক গড় বৃদ্ধি, খাদ্য, এফসিআর ও প্রতি কেজি বৃদ্ধির খরচ।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalGrowthReportRow>> Columns =>
    [
        ReportColumn<AnimalGrowthReportRow>.Date("date", new LocalizedText("Weigh-in", "ওজনের তারিখ"), r => r.Date),
        ReportColumn<AnimalGrowthReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, 1, 22f),
        ReportColumn<AnimalGrowthReportRow>.WholeNumber("days", new LocalizedText("Days", "দিন"), r => r.IntervalDays, 14f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Number("gain", new LocalizedText("Gain (kg)", "বৃদ্ধি (কেজি)"), r => r.GainKg, 2, 20f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Number("adg", new LocalizedText("ADG (kg/d)", "দৈনিক বৃদ্ধি"), r => r.AdgKg, 3, 20f),
        ReportColumn<AnimalGrowthReportRow>.Number("feed", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.FeedKg, 2, 20f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Money("feedCost", new LocalizedText("Feed cost", "খাদ্য খরচ"), r => r.FeedCostBdt, 2, 22f),
        ReportColumn<AnimalGrowthReportRow>.Number("fcr", new LocalizedText("FCR", "এফসিআর"), r => r.Fcr, 2, 16f),
        ReportColumn<AnimalGrowthReportRow>.Money("costPerKg", new LocalizedText("Cost / kg gain", "প্রতি কেজি খরচ"), r => r.CostPerKgGainBdt, 2, 24f, ReportAggregate.None),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await _animals.GetByIdAsync(context.Id("animalId"), cancellationToken).ConfigureAwait(false);
        return animal is null
            ? null
            : $"Tag {animal.Tag.TagId} · {animal.Species} · {animal.Sex} · {animal.Status}";
    }

    protected override async Task<IReadOnlyList<AnimalGrowthReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var period = context.Range("period");

        var animal = await _animals.GetByIdWithWeightsAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return [];
        }

        var weighings = animal.WeightRecords
            .Where(w => period.Contains(w.RecordedDate))
            .OrderBy(w => w.RecordedDate)
            .ToList();

        if (weighings.Count == 0)
        {
            return [];
        }

        var allocations = await _allocations
            .GetByAnimalAsync(animalId, period.From, period.To, cancellationToken)
            .ConfigureAwait(false);

        List<(DateOnly EntryDate, decimal AllocatedKg, decimal AllocatedCostBdt)> feedRecords;

        if (allocations.Count > 0)
        {
            feedRecords = allocations
                .Select(a => (a.EntryDate, a.AllocatedKg, a.AllocatedCostBdt))
                .ToList();
        }
        else
        {
            // Resilient fallback: If allocations are not yet populated, read individual plan entries directly
            var fallbackEntries = await _dailyEntries
                .GetEntriesByAnimalIdAsync(animal.TenantId, animalId, cancellationToken)
                .ConfigureAwait(false);

            feedRecords = fallbackEntries
                .Where(e => e.EntryDate >= period.From && e.EntryDate <= period.To)
                .Select(e =>
                {
                    var kg = e.ActualKg ?? e.ExpectedKg;
                    var cost = e.TotalCostBdt ?? Math.Round(kg * (e.UnitCostAtConsumptionBdt ?? 0m), 2);
                    return (e.EntryDate, kg, cost);
                })
                .ToList();
        }

        var rows = new List<AnimalGrowthReportRow>(weighings.Count);
        DateOnly? previousDate = null;
        decimal? previousWeight = null;

        foreach (var weighing in weighings)
        {
            var weight = weighing.Weight.WeightKg;

            // The first weigh-in opens the series: there is no prior weight to measure a gain
            // against, so its interval columns are deliberately zero rather than misleading.
            var days = previousDate is null ? 0 : weighing.RecordedDate.DayNumber - previousDate.Value.DayNumber;
            var gain = previousWeight is null ? 0m : weight - previousWeight.Value;

            var intervalFeed = previousDate is null
                ? []
                : feedRecords
                    .Where(a => a.EntryDate > previousDate.Value && a.EntryDate <= weighing.RecordedDate)
                    .ToList();

            var feedKg = intervalFeed.Sum(a => a.AllocatedKg);
            var feedCost = intervalFeed.Sum(a => a.AllocatedCostBdt);

            rows.Add(new AnimalGrowthReportRow(
                weighing.RecordedDate,
                weight,
                days,
                gain,
                days > 0 ? decimal.Round(gain / days, 3, MidpointRounding.AwayFromZero) : 0m,
                feedKg,
                feedCost,

                // Guard the divisions: an animal that lost or held weight has no meaningful FCR,
                // and printing a vast number or a crash helps nobody.
                gain > 0 ? decimal.Round(feedKg / gain, 2, MidpointRounding.AwayFromZero) : 0m,
                gain > 0 ? decimal.Round(feedCost / gain, 2, MidpointRounding.AwayFromZero) : 0m));

            previousDate = weighing.RecordedDate;
            previousWeight = weight;
        }

        return rows;
    }

    /// <summary>Formats an interval for a log line or a tooltip.</summary>
    public static string DescribeInterval(DateOnly from, DateOnly to) =>
        $"{from.ToString("dd-MMM", CultureInfo.InvariantCulture)} → {to.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)}";
}
