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

    public override LocalizedText Title => new("Animal Daily Cost Report", "পশুর দৈনিক খরচ প্রতিবেদন");

    public override LocalizedText Description => new(
        "Day-by-day cost of one animal — feed, vet & medicine, labour and overhead — with the running cost since it arrived.",
        "একটি পশুর দিনভিত্তিক খরচ — খাদ্য, চিকিৎসা, শ্রম ও পরোক্ষ খরচ — এবং আগমন থেকে ক্রমযোজিত খরচ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "সময়কালের মোট খরচ");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
    ];

    public override IReadOnlyList<ReportColumn<AnimalDailyCostReportRow>> Columns =>
    [
        ReportColumn<AnimalDailyCostReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<AnimalDailyCostReportRow>.Number("feedKg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য খরচ"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("vet", new LocalizedText("Vet & Medicine (BDT)", "চিকিৎসা ও ওষুধ"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("total", new LocalizedText("Day Total (BDT)", "দিনের মোট"), r => r.TotalBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostReportRow>.Money("running", new LocalizedText("Cost Since Arrival (BDT)", "আগমন থেকে মোট"), r => r.RunningBdt, widthMm: 32f),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return null;
        }

        var period = context.Range("period");
        var (from, to) = DailyAnimalCostCalculator.OnFarmSpan(animal);
        var exit = to == DateOnly.MaxValue ? string.Empty : $" · Left {to:dd-MMM-yyyy}";
        return $"Tag {animal.Tag.TagId} · {animal.Species} · {animal.Sex} · {animal.Status} · Arrived {from:dd-MMM-yyyy}{exit} · Period {period}";
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
