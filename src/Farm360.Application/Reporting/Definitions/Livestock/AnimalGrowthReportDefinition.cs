using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One weigh-in and what happened in the interval leading up to it.</summary>
/// <remarks>
/// The interval figures are null on the first weigh-in of the range (there is nothing earlier to
/// measure against) and the efficiency figures are null when the animal did not gain weight or no
/// feed was confirmed — a blank cell, never a misleading zero.
/// </remarks>
public sealed record AnimalGrowthReportRow(
    DateOnly Date,
    decimal WeightKg,
    int IntervalDays,
    decimal? GainKg,
    decimal? AdgKg,
    decimal FeedKg,
    decimal FeedCostBdt,
    decimal? Fcr,
    decimal? CostPerKgGainBdt,
    string? WeighedBy = null);

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

    public AnimalGrowthReportDefinition(
        IAnimalRepository animals,
        IAnimalFeedAllocationRepository allocations)
    {
        _animals = animals;
        _allocations = allocations;
    }

    public override string Key => "livestock.animal-growth";

    public override LocalizedText Title => new("Animal Weight & Growth", "পশুর ওজন ও বৃদ্ধি");

    public override LocalizedText Description => new(
        "Is this animal growing well for the feed it eats? Every weigh-in with the weight gained since the last one, the feed eaten in between and what each kg of gain cost.",
        "পশুটি খাওয়া খাদ্যের তুলনায় ঠিকমতো বাড়ছে কি? প্রতিটি ওজনের তারিখে আগের ওজনের পর কত কেজি বেড়েছে, মাঝের সময়ে কত খাদ্য খেয়েছে এবং প্রতি কেজি বৃদ্ধিতে কত খরচ হয়েছে।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalGrowthReportRow>> Columns =>
    [
        ReportColumn<AnimalGrowthReportRow>.Date("date", new LocalizedText("Weigh-in date", "ওজনের তারিখ"), r => r.Date, 24f),
        ReportColumn<AnimalGrowthReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, 1, 22f),
        // "(tape)" when the weight was estimated from a tape measure; blank for a scale weight.
        ReportColumn<AnimalGrowthReportRow>.Text("weighedBy", new LocalizedText("Measured by", "মাপার ধরন"), r => r.WeighedBy, 16f, relative: false),
        ReportColumn<AnimalGrowthReportRow>.WholeNumber("days", new LocalizedText("Days since last weigh-in", "আগের ওজনের পর (দিন)"), r => r.IntervalDays, 24f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Number("gain", new LocalizedText("Weight gained (kg)", "ওজন বৃদ্ধি (কেজি)"), r => r.GainKg, 1, 24f, ReportAggregate.Sum),
        // Period daily gain (between consecutive weigh-ins inside the selected date range) --
        // distinct from the lifetime daily gain on the herd/animal summaries, which uses the
        // oldest-to-newest weight record ever and will legitimately disagree with this one.
        ReportColumn<AnimalGrowthReportRow>.Number("adg", new LocalizedText("Daily gain (kg/day)", "দৈনিক বৃদ্ধি (কেজি/দিন)"), r => r.AdgKg, 3, 24f),
        ReportColumn<AnimalGrowthReportRow>.Number("feed", new LocalizedText("Feed eaten (kg)", "খাওয়া খাদ্য (কেজি)"), r => r.FeedKg, 1, 24f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Money("feedCost", new LocalizedText("Feed cost (BDT)", "খাদ্য খরচ (টাকা)"), r => r.FeedCostBdt, 2, 26f, ReportAggregate.Sum),
        ReportColumn<AnimalGrowthReportRow>.Number("fcr", new LocalizedText("Feed per kg gain (kg)", "প্রতি কেজি বৃদ্ধিতে খাদ্য (কেজি)"), r => r.Fcr, 2, 26f),
        ReportColumn<AnimalGrowthReportRow>.Money("costPerKg", new LocalizedText("Feed cost per kg gain (BDT)", "প্রতি কেজি বৃদ্ধিতে খাদ্য খরচ (টাকা)"), r => r.CostPerKgGainBdt, 2, 28f, ReportAggregate.None),
    ];

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "সময়কালের মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one weigh-in; the gain, feed and cost columns cover the days since the previous weigh-in.",
            "প্রতিটি সারি একটি ওজন নেওয়ার দিন; বৃদ্ধি, খাদ্য ও খরচের ঘর আগের ওজনের পর থেকে এই দিন পর্যন্ত সময়ের হিসাব।"),
        new("The first weigh-in in the period has nothing earlier to compare with, so its gain and efficiency cells are blank.",
            "সময়কালের প্রথম ওজনের আগে তুলনা করার মতো ওজন নেই, তাই তার বৃদ্ধি ও দক্ষতার ঘর ফাঁকা থাকে।"),
        new("Daily gain = weight gained ÷ days. Feed per kg gain = feed eaten ÷ weight gained — lower is better.",
            "দৈনিক বৃদ্ধি = ওজন বৃদ্ধি ÷ দিন। প্রতি কেজি বৃদ্ধিতে খাদ্য = খাওয়া খাদ্য ÷ ওজন বৃদ্ধি — যত কম তত ভালো।"),
        new("(tape) = the weight was estimated from a tape measure (length × girth² ÷ 660, in inches), usually within about ±5–10% of a scale.",
            "(ফিতা) = ওজনটি ফিতা দিয়ে মেপে হিসাব করা (দৈর্ঘ্য × বেড়² ÷ 660, ইঞ্চিতে), সাধারণত ওজন মেশিনের চেয়ে ±5–10% কম-বেশি হতে পারে।"),
        new("Only confirmed feed is counted, not planned rations. If the animal lost weight or no feed was confirmed, the efficiency cells are blank.",
            "শুধু নিশ্চিত করা খাদ্য গণনা হয়েছে, পরিকল্পিত রেশন নয়। ওজন কমলে বা খাদ্য নিশ্চিত না হলে দক্ষতার ঘর ফাঁকা থাকে।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await _animals.GetByIdAsync(context.Id("animalId"), cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return null;
        }

        var bn = context.Language == ReportLanguage.Bn;
        var period = context.Range("period");
        return $"{(bn ? "ট্যাগ" : "Tag")} {AnimalLabels.Tag(animal, context)} · {SpeciesLabel(animal.Species, bn)} · {SexLabel(animal.Sex, bn)} · {DescribePeriod(period)}";
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

        // Confirmed allocations only. There is deliberately no fallback to the plan's expected
        // kilograms: a planned ration is not feed eaten, and an FCR computed from it flatters the
        // animal by exactly the feed that was never confirmed. An interval with no confirmed feed
        // shows 0 kg and no FCR — "not recorded" — rather than a number that looks measured.
        var feedRecords = allocations
            .Select(a => (a.EntryDate, a.AllocatedKg, a.AllocatedCostBdt))
            .ToList();

        var tapeLabel = context.Language == ReportLanguage.Bn ? "(ফিতা)" : "(tape)";
        var rows = new List<AnimalGrowthReportRow>(weighings.Count);
        DateOnly? previousDate = null;
        decimal? previousWeight = null;

        foreach (var weighing in weighings)
        {
            var weight = weighing.Weight.WeightKg;

            // The first weigh-in opens the series: there is no prior weight to measure a gain
            // against, so its gain is blank rather than a misleading zero.
            var days = previousDate is null ? 0 : weighing.RecordedDate.DayNumber - previousDate.Value.DayNumber;
            decimal? gain = previousWeight is null ? null : weight - previousWeight.Value;

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
                gain is { } g && days > 0 ? decimal.Round(g / days, 3, MidpointRounding.AwayFromZero) : null,
                feedKg,
                feedCost,

                // Guard the divisions: an animal that lost or held weight, or an interval with no
                // confirmed feed, has no meaningful efficiency: a blank cell, not a vast number or 0.
                gain is { } g1 && g1 > 0 && feedKg > 0 ? decimal.Round(feedKg / g1, 2, MidpointRounding.AwayFromZero) : null,
                gain is { } g2 && g2 > 0 && feedCost > 0 ? decimal.Round(feedCost / g2, 2, MidpointRounding.AwayFromZero) : null,
                weighing.Method == WeightMethod.Tape ? tapeLabel : null));

            previousDate = weighing.RecordedDate;
            previousWeight = weight;
        }

        return rows;
    }

    private static string DescribePeriod(DateRange period) =>
        $"{period.From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {period.To.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

    private static string SpeciesLabel(AnimalSpecies species, bool bn) => species switch
    {
        AnimalSpecies.CattleBeef => bn ? "মাংসের গরু" : "Beef cattle",
        AnimalSpecies.CattleDairy => bn ? "দুগ্ধ গরু" : "Dairy cattle",
        AnimalSpecies.Goat => bn ? "ছাগল" : "Goat",
        AnimalSpecies.Sheep => bn ? "ভেড়া" : "Sheep",
        _ => species.ToString(),
    };

    private static string SexLabel(AnimalSex sex, bool bn) => sex switch
    {
        AnimalSex.Male => bn ? "পুরুষ" : "Male",
        AnimalSex.Female => bn ? "স্ত্রী" : "Female",
        _ => sex.ToString(),
    };

    /// <summary>Formats an interval for a log line or a tooltip.</summary>
    public static string DescribeInterval(DateOnly from, DateOnly to) =>
        $"{from.ToString("dd-MMM", CultureInfo.InvariantCulture)} → {to.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)}";
}
