using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record HerdDailyCostReportRow(
    DateOnly Date,
    int HeadCount,
    decimal FeedKg,
    decimal FeedBdt,
    decimal VetBdt,
    decimal LabourBdt,
    decimal OverheadBdt,
    decimal TotalBdt,
    decimal CostPerHeadBdt);

/// <summary>
/// Report FN7: what all the animals on a farm, or in one batch, cost each day. Each line shows the
/// day's head count, the cost in each bucket, and the cost per head. A per-animal breakdown is in
/// <see cref="AnimalDailyCostByAnimalReportDefinition"/>.
/// </summary>
public sealed class HerdDailyCostReportDefinition : ReportDefinition<HerdDailyCostReportRow>
{
    private readonly DailyAnimalCostScope _scope;
    private readonly DailyAnimalCostCalculator _calculator;

    public HerdDailyCostReportDefinition(DailyAnimalCostScope scope, DailyAnimalCostCalculator calculator)
    {
        _scope = scope;
        _calculator = calculator;
    }

    public override string Key => "finance.herd-daily-cost";

    public override LocalizedText Title => new("Daily Cost of the Herd", "পুরো পালের দৈনিক খরচ");

    public override LocalizedText Description => new(
        "How much the whole herd (or one batch) cost each day — feed, vet & medicine, labour and overhead — and what that comes to per animal.",
        "পুরো পাল (বা একটি ব্যাচ) প্রতিদিন কত খরচ করেছে — খাদ্য, চিকিৎসা ও ওষুধ, শ্রম ও পরোক্ষ খরচ — এবং পশুপ্রতি কত পড়েছে।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total cost for the period", "সময়কালের মোট খরচ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each line is one calendar day; days with no cost still appear so gaps are visible.",
            "প্রতিটি সারি একটি দিন; খরচ না থাকলেও দিনটি দেখানো হয়েছে, যাতে ফাঁক বোঝা যায়।"),
        new("Animals = head on the farm that day. A sold or dead animal stops counting (and costing) the day before it left.",
            "পশুর সংখ্যা = সেদিন খামারে থাকা পশু। বিক্রি বা মৃত পশু যেদিন খামার ছাড়ে তার আগের দিন পর্যন্ত গণনা ও খরচে থাকে।"),
        new("Feed is what was actually fed that day; vet & medicine is booked on the day of treatment or vaccination.",
            "খাদ্য খরচ সেদিন আসলে যা খাওয়ানো হয়েছে; চিকিৎসা ও ওষুধ খরচ চিকিৎসা বা টিকার দিনে ধরা হয়েছে।"),
        new("Labour and overhead bills are shared among animals by the days each was on the farm, then spread evenly over those days.",
            "শ্রম ও পরোক্ষ খরচ প্রতিটি পশুর খামারে থাকার দিন অনুযায়ী ভাগ করে সেই দিনগুলোতে সমানভাবে ছড়ানো হয়েছে।"),
        new("Cost per animal = total for the day ÷ animals that day. Purchase prices are not included.",
            "পশুপ্রতি খরচ = দিনের মোট খরচ ÷ সেদিনের পশুর সংখ্যা। পশুর ক্রয়মূল্য এখানে ধরা হয়নি।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
        ReportParameter.Batch(name: "batchId", required: false),
    ];

    // Column order (Date, Animals, …, Total, Per animal) is relied on by DailyAnimalCostReportTests.
    public override IReadOnlyList<ReportColumn<HerdDailyCostReportRow>> Columns =>
    [
        ReportColumn<HerdDailyCostReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<HerdDailyCostReportRow>.WholeNumber("head", new LocalizedText("Animals (head)", "পশু (টি)"), r => r.HeadCount, widthMm: 20f),
        ReportColumn<HerdDailyCostReportRow>.Number("feedKg", new LocalizedText("Feed fed (kg)", "খাওয়ানো খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য (টাকা)"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("vet", new LocalizedText("Vet & medicine (BDT)", "চিকিৎসা ও ওষুধ (টাকা)"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম (টাকা)"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ (টাকা)"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("total", new LocalizedText("Total for the day (BDT)", "দিনের মোট (টাকা)"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("perHead", new LocalizedText("Per animal (BDT/head)", "পশুপ্রতি (টাকা/টি)"), r => r.CostPerHeadBdt, widthMm: 28f),
    ];

    public override ReportGroup<HerdDailyCostReportRow>? Group =>
        ReportGroup<HerdDailyCostReportRow>.ByLocalized(
            r => AnimalCostReportText.MonthKey(r.Date),
            r => AnimalCostReportText.MonthLabel(r.Date));

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{scope.Label} · {AnimalCostReportText.Period(scope.Period)}";
    }

    protected override async Task<IReadOnlyList<HerdDailyCostReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var period = scope.Period;
        if (period.To < period.From)
        {
            return [];
        }

        var costs = await _calculator
            .CalculateAsync(scope.FarmId, period, scope.AnimalsById, scope.Filter, cancellationToken)
            .ConfigureAwait(false);
        var byDate = costs.GroupBy(c => c.Date).ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<HerdDailyCostReportRow>(period.Days);
        for (var d = period.From; d <= period.To; d = d.AddDays(1))
        {
            var day = byDate.GetValueOrDefault(d) ?? [];
            var onFarm = scope.Animals.Count(a => DailyAnimalCostCalculator.IsOnFarm(a, d));

            // An animal that has costs on a day it is not recorded as present is counted anyway.
            // For example, it was fed before its arrival date was entered.
            var unrecorded = day.Select(c => c.AnimalId).Distinct().Count(id =>
                !scope.AnimalsById.TryGetValue(id, out var a) || !DailyAnimalCostCalculator.IsOnFarm(a, d));
            var head = onFarm + unrecorded;

            var total = day.Sum(c => c.TotalBdt);
            rows.Add(new HerdDailyCostReportRow(
                d,
                head,
                day.Sum(c => c.FeedKg),
                day.Sum(c => c.FeedBdt),
                day.Sum(c => c.VetBdt),
                day.Sum(c => c.LabourBdt),
                day.Sum(c => c.OverheadBdt),
                total,
                head > 0 ? Math.Round(total / head, 2, MidpointRounding.AwayFromZero) : 0m));
        }

        return rows;
    }
}

/// <summary>
/// The animals a farm- or batch-wide daily cost report covers. These are the animals on the farm
/// at any time in the period, including those sold or dead during it, narrowed to one batch if
/// one is chosen. The period stops at today.
/// </summary>
public sealed class DailyAnimalCostScope
{
    private readonly IAnimalRepository _animals;
    private readonly IAnimalBatchRepository _batches;

    public DailyAnimalCostScope(IAnimalRepository animals, IAnimalBatchRepository batches)
    {
        _animals = animals;
        _batches = batches;
    }

    public sealed record Result(
        Guid FarmId,
        DateRange Period,
        IReadOnlyList<Animal> Animals,
        IReadOnlyDictionary<Guid, Animal> AnimalsById,
        IReadOnlySet<Guid>? Filter,
        string Label);

    public Task<Result> ResolveAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.MemoAsync("daily-cost-scope", () => LoadAsync(context, cancellationToken));
    }

    private async Task<Result> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        var requested = context.Range("period");
        var period = new DateRange(requested.From, requested.To < context.Today ? requested.To : context.Today);
        var batchId = context.IdOrNull("batchId");

        var present = period.To < period.From
            ? []
            : await _animals.GetPresentDuringPeriodAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Animal> animals = batchId is { } b ? present.Where(a => a.BatchId == b).ToList() : present;
        var byId = animals.ToDictionary(a => a.Id);

        var bn = context.Language == ReportLanguage.Bn;
        var label = bn ? "খামারের সকল পশু" : "All animals on the farm";
        HashSet<Guid>? filter = null;
        if (batchId is { } id)
        {
            var batch = await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            var name = batch?.Name ?? (bn ? "অজানা" : "unknown");
            label = bn ? $"ব্যাচ {name}" : $"Batch {name}";
            filter = [.. byId.Keys];
        }

        return new Result(farmId, period, animals, byId, filter, label);
    }
}

/// <summary>
/// Plain-language wording shared by the animal cost reports in this folder: species and sex in
/// words, readable periods ("1 Nov – 18 Nov 2026"), month bands and the one-line animal subject.
/// </summary>
internal static class AnimalCostReportText
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Day(DateOnly date) => date.ToString("d MMM yyyy", Invariant);

    /// <summary>"18 Nov 2026", "1 Nov – 18 Nov 2026" or "20 Dec 2025 – 18 Jan 2026".</summary>
    public static string Period(DateRange range)
    {
        if (range.From == range.To)
        {
            return Day(range.From);
        }

        var from = range.From.Year == range.To.Year ? range.From.ToString("d MMM", Invariant) : Day(range.From);
        return from + " – " + Day(range.To);
    }

    public static string MonthKey(DateOnly date) => date.ToString("yyyy-MM", Invariant);

    public static LocalizedText MonthLabel(DateOnly date)
    {
        var month = date.ToString("MMM yyyy", Invariant);
        return new LocalizedText("Month: " + month, "মাস: " + month);
    }

    public static string Species(AnimalSpecies species, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return species switch
        {
            AnimalSpecies.CattleBeef => bn ? "মাংসের গরু" : "Beef cattle",
            AnimalSpecies.CattleDairy => bn ? "দুধের গাভী" : "Dairy cattle",
            AnimalSpecies.Goat => bn ? "ছাগল" : "Goat",
            AnimalSpecies.Sheep => bn ? "ভেড়া" : "Sheep",
            _ => species.ToString(),
        };
    }

    public static string Sex(AnimalSex sex, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return sex switch
        {
            AnimalSex.Male => bn ? "পুরুষ" : "Male",
            AnimalSex.Female => bn ? "স্ত্রী" : "Female",
            _ => sex.ToString(),
        };
    }

    /// <summary>
    /// "Tag B-04 · Beef cattle · Male · on farm since 21 Aug 2026", or for an animal that has left,
    /// "Tag B-04 (Sold 12-Nov-2026) · … · on farm 21 Aug 2026 – 11 Nov 2026".
    /// </summary>
    public static string AnimalLine(Animal animal, ReportLanguage language, string? breed = null)
    {
        ArgumentNullException.ThrowIfNull(animal);
        var bn = language == ReportLanguage.Bn;
        var parts = new List<string>
        {
            (bn ? "ট্যাগ " : "Tag ") + AnimalLabels.Tag(animal, language, bengaliNumerals: false),
            string.IsNullOrWhiteSpace(breed) ? Species(animal.Species, language) : breed!,
            Sex(animal.Sex, language),
        };

        var (from, to) = DailyAnimalCostCalculator.OnFarmSpan(animal);
        if (!animal.HasLeftFarm)
        {
            parts.Add(bn ? Day(from) + " থেকে খামারে" : "on farm since " + Day(from));
        }
        else if (to >= from)
        {
            parts.Add(bn ? "খামারে ছিল " + Day(from) + " – " + Day(to) : "on farm " + Day(from) + " – " + Day(to));
        }

        return string.Join(" · ", parts);
    }
}
