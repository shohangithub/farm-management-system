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

    public override LocalizedText Title => new("Herd Daily Cost Report", "পালের দৈনিক খরচ প্রতিবেদন");

    public override LocalizedText Description => new(
        "Day-by-day cost of all animals on the farm (or one batch): head count, feed, vet & medicine, labour, overhead and cost per head.",
        "খামারের (বা একটি ব্যাচের) সকল পশুর দিনভিত্তিক খরচ: পশুর সংখ্যা, খাদ্য, চিকিৎসা, শ্রম, পরোক্ষ খরচ ও মাথাপিছু খরচ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "সময়কালের মোট খরচ");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
        ReportParameter.Batch(name: "batchId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<HerdDailyCostReportRow>> Columns =>
    [
        ReportColumn<HerdDailyCostReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<HerdDailyCostReportRow>.WholeNumber("head", new LocalizedText("Animals", "পশুর সংখ্যা"), r => r.HeadCount, widthMm: 18f),
        ReportColumn<HerdDailyCostReportRow>.Number("feedKg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য খরচ"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("vet", new LocalizedText("Vet & Medicine (BDT)", "চিকিৎসা ও ওষুধ"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("total", new LocalizedText("Day Total (BDT)", "দিনের মোট"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<HerdDailyCostReportRow>.Money("perHead", new LocalizedText("Cost / Head (BDT)", "মাথাপিছু খরচ"), r => r.CostPerHeadBdt, widthMm: 26f),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{scope.Label} · Period {context.Range("period")}";
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

        var label = "All animals on the farm";
        HashSet<Guid>? filter = null;
        if (batchId is { } id)
        {
            var batch = await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            label = $"Batch {batch?.Name ?? "(not found)"}";
            filter = [.. byId.Keys];
        }

        return new Result(farmId, period, animals, byId, filter, label);
    }
}
