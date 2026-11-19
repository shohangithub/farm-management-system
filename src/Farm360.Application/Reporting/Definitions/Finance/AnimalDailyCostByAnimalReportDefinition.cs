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

public sealed record AnimalDailyCostByAnimalReportRow(
    string GroupKey,
    string GroupLabel,
    DateOnly Date,
    decimal FeedKg,
    decimal FeedBdt,
    decimal VetBdt,
    decimal LabourBdt,
    decimal OverheadBdt,
    decimal TotalBdt);

/// <summary>
/// Report FN8: what each animal cost each day, for every animal on the farm or in one batch. The
/// lines are grouped by animal, and each animal ends with a subtotal. Days on which an animal had
/// no cost are left out to keep the report short.
/// </summary>
public sealed class AnimalDailyCostByAnimalReportDefinition : ReportDefinition<AnimalDailyCostByAnimalReportRow>
{
    private readonly DailyAnimalCostScope _scope;
    private readonly DailyAnimalCostCalculator _calculator;
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IAnimalBatchRepository _batches;

    public AnimalDailyCostByAnimalReportDefinition(
        DailyAnimalCostScope scope,
        DailyAnimalCostCalculator calculator,
        IAnimalRepository animals,
        IBreedRepository breeds,
        IAnimalBatchRepository batches)
    {
        _scope = scope;
        _calculator = calculator;
        _animals = animals;
        _breeds = breeds;
        _batches = batches;
    }

    public override string Key => "finance.animal-daily-cost-all";

    public override LocalizedText Title => new("Daily Cost of Every Animal", "প্রতিটি পশুর দৈনিক খরচ");

    public override LocalizedText Description => new(
        "Each animal's cost day by day — feed, vet & medicine, labour and overhead — grouped by animal with a subtotal for each.",
        "প্রতিটি পশুর দিনভিত্তিক খরচ — খাদ্য, চিকিৎসা, শ্রম ও পরোক্ষ খরচ — পশুভিত্তিক উপমোটসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total for all animals", "সকল পশুর মোট খরচ");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
        ReportParameter.Batch(name: "batchId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<AnimalDailyCostByAnimalReportRow>> Columns =>
    [
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Number("feedKg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য খরচ"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("vet", new LocalizedText("Vet & Medicine (BDT)", "চিকিৎসা ও ওষুধ"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("total", new LocalizedText("Day Total (BDT)", "দিনের মোট"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<AnimalDailyCostByAnimalReportRow>? Group =>
        ReportGroup<AnimalDailyCostByAnimalReportRow>.By(r => r.GroupKey, r => r.GroupLabel);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{scope.Label} · Period {context.Range("period")}";
    }

    protected override async Task<IReadOnlyList<AnimalDailyCostByAnimalReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        if (scope.Period.To < scope.Period.From)
        {
            return [];
        }

        var costs = await _calculator
            .CalculateAsync(scope.FarmId, scope.Period, scope.AnimalsById, scope.Filter, cancellationToken)
            .ConfigureAwait(false);
        if (costs.Count == 0)
        {
            return [];
        }

        // Animals with costs but outside the "present" list (data entered before the arrival date).
        var animalsById = new Dictionary<Guid, Animal>(scope.AnimalsById);
        var missing = costs.Select(c => c.AnimalId).Distinct().Where(id => !animalsById.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            foreach (var a in await _animals.GetByIdsAsync(missing, cancellationToken).ConfigureAwait(false))
            {
                animalsById[a.Id] = a;
            }
        }

        var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(b => b.Id, b => b.Name);
        var batchNames = new Dictionary<Guid, string>();
        foreach (var batchId in animalsById.Values.Where(a => a.BatchId.HasValue).Select(a => a.BatchId!.Value).Distinct())
        {
            var batch = await _batches.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (batch is not null)
            {
                batchNames[batchId] = batch.Name;
            }
        }

        string TagOf(Guid id) => animalsById.TryGetValue(id, out var a) ? AnimalLabels.Tag(a) : "(animal not found)";
        string LabelOf(Guid id)
        {
            if (!animalsById.TryGetValue(id, out var a))
            {
                return "(animal not found)";
            }

            var breed = breeds.GetValueOrDefault(a.BreedId) ?? a.Species.ToString();
            var batch = a.BatchId is { } b && batchNames.TryGetValue(b, out var name) ? $" · Batch {name}" : string.Empty;
            return $"Tag {a.Tag.TagId} · {breed} · {a.Sex} · {a.Status}{batch}";
        }

        return costs
            .OrderBy(c => TagOf(c.AnimalId), StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.AnimalId)
            .ThenBy(c => c.Date)
            .Select(c => new AnimalDailyCostByAnimalReportRow(
                c.AnimalId.ToString(),
                LabelOf(c.AnimalId),
                c.Date,
                c.FeedKg,
                c.FeedBdt,
                c.VetBdt,
                c.LabourBdt,
                c.OverheadBdt,
                c.TotalBdt))
            .ToList();
    }
}
