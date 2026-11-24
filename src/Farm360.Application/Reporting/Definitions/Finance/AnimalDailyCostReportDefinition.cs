using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record AnimalDailyCostReportRow(
    DateOnly Date,
    decimal FeedKg,
    decimal FeedBdt,
    decimal VetBdt,
    decimal LabourBdt,
    decimal OverheadBdt,
    decimal TotalBdt,
    decimal RunningBdt);

/// <summary>
/// Report FN6: what one animal cost each day. There is one line for every day it was on the farm
/// in the period, including days with no cost. Each line shows the day's cost by bucket and the
/// running cost since the animal arrived. The purchase price is not included.
/// </summary>
public sealed class AnimalDailyCostReportDefinition : ReportDefinition<AnimalDailyCostReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly DailyAnimalCostCalculator _calculator;

    public AnimalDailyCostReportDefinition(IAnimalRepository animals, DailyAnimalCostCalculator calculator)
    {
        _animals = animals;
        _calculator = calculator;
    }

    public override string Key => "finance.animal-daily-cost";

    public override LocalizedText Title => new("Daily Cost of One Animal", "একটি পশুর দৈনিক খরচ");

    public override LocalizedText Description => new(
        "What one animal cost each day — feed, vet & medicine, labour and overhead — and how much it has cost in total since it arrived.",
        "একটি পশুর প্রতিদিনের খরচ — খাদ্য, চিকিৎসা ও ওষুধ, শ্রম ও পরোক্ষ খরচ — এবং খামারে আসার পর থেকে মোট কত খরচ হয়েছে।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total cost for the period", "সময়কালের মোট খরচ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each line is one day the animal was on the farm in the period, including days with no cost.",
            "প্রতিটি সারি সময়কালের মধ্যে পশুটির খামারে থাকা একটি দিন, খরচ না থাকলেও।"),
        new("Feed is what the animal was actually fed that day; vet & medicine is booked on the day of treatment or vaccination.",
            "খাদ্য খরচ সেদিন আসলে যা খাওয়ানো হয়েছে; চিকিৎসা ও ওষুধ খরচ চিকিৎসা বা টিকার দিনে ধরা হয়েছে।"),
        new("Labour and overhead are shared among animals by the days each was on the farm, then spread evenly over those days.",
            "শ্রম ও পরোক্ষ খরচ প্রতিটি পশুর খামারে থাকার দিন অনুযায়ী ভাগ করে সেই দিনগুলোতে সমানভাবে ছড়ানো হয়েছে।"),
        new("Cost since arrival is the running total from the day it came to the farm, so it includes days before this period. The purchase price is not included.",
            "আগমন থেকে মোট খরচ = খামারে আসার দিন থেকে চলমান যোগফল, তাই এই সময়ের আগের দিনগুলোও এতে আছে। ক্রয়মূল্য ধরা হয়নি।"),
        new("A sold or dead animal's costs stop the day before it left the farm.",
            "বিক্রি বা মৃত পশুর খরচ খামার ছাড়ার আগের দিন পর্যন্ত ধরা হয়েছে।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
    ];

    // Column positions (day total = 6, running = 7) are relied on by DailyAnimalCostReportTests.
    public override IReadOnlyList<ReportColumn<AnimalDailyCostReportRow>> Columns =>
    [
        ReportColumn<AnimalDailyCostReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<AnimalDailyCostReportRow>.Number("feedKg", new LocalizedText("Feed fed (kg)", "খাওয়ানো খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য (টাকা)"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("vet", new LocalizedText("Vet & medicine (BDT)", "চিকিৎসা ও ওষুধ (টাকা)"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম (টাকা)"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ (টাকা)"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("total", new LocalizedText("Total for the day (BDT)", "দিনের মোট (টাকা)"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("running", new LocalizedText("Cost since arrival (BDT)", "আগমন থেকে মোট খরচ (টাকা)"), r => r.RunningBdt, widthMm: 32f),
    ];

    public override ReportGroup<AnimalDailyCostReportRow>? Group =>
        ReportGroup<AnimalDailyCostReportRow>.ByLocalized(
            r => AnimalCostReportText.MonthKey(r.Date),
            r => AnimalCostReportText.MonthLabel(r.Date));

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return null;
        }

        return AnimalCostReportText.AnimalLine(animal, context.Language)
            + " · " + AnimalCostReportText.Period(context.Range("period"));
    }

    protected override async Task<IReadOnlyList<AnimalDailyCostReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return [];
        }

        var period = context.Range("period");
        var (arrived, left) = DailyAnimalCostCalculator.OnFarmSpan(animal);

        // Lines run from the later of period start and arrival to the earliest of period end, exit and today.
        var first = period.From > arrived ? period.From : arrived;
        var last = new[] { period.To, left, context.Today }.Min();
        if (last < first)
        {
            return [];
        }

        // Calculated from arrival, so the running column shows the full cost to date, not just the period's.
        var calcFrom = arrived < first ? arrived : first;
        var costs = await _calculator.CalculateAsync(
                animal.FarmId,
                new DateRange(calcFrom, last),
                new Dictionary<Guid, Animal> { [animal.Id] = animal },
                new HashSet<Guid> { animal.Id },
                cancellationToken)
            .ConfigureAwait(false);

        var byDate = costs.ToDictionary(c => c.Date);
        var running = costs.Where(c => c.Date < first).Sum(c => c.TotalBdt);

        var rows = new List<AnimalDailyCostReportRow>(last.DayNumber - first.DayNumber + 1);
        for (var d = first; d <= last; d = d.AddDays(1))
        {
            byDate.TryGetValue(d, out var c);
            var total = c?.TotalBdt ?? 0m;
            running += total;
            rows.Add(new AnimalDailyCostReportRow(
                d,
                c?.FeedKg ?? 0m,
                c?.FeedBdt ?? 0m,
                c?.VetBdt ?? 0m,
                c?.LabourBdt ?? 0m,
                c?.OverheadBdt ?? 0m,
                total,
                running));
        }

        return rows;
    }

    private Task<Animal?> LoadAnimalAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("animal", () => _animals.GetByIdAsync(context.Id("animalId"), cancellationToken));
}
