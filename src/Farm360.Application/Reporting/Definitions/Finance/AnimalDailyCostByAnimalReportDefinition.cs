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
        "What each animal cost day by day — feed, vet & medicine, labour and overhead — with a total for each animal.",
        "প্রতিটি পশুর দিনভিত্তিক খরচ — খাদ্য, চিকিৎসা ও ওষুধ, শ্রম ও পরোক্ষ খরচ — প্রতিটি পশুর আলাদা মোটসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total cost for all animals", "সকল পশুর মোট খরচ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Lines are grouped by animal; each line is one day, and the sub-total under each animal is what it cost in the period.",
            "সারিগুলো পশু অনুযায়ী সাজানো; প্রতিটি সারি একটি দিন, আর প্রতিটি পশুর নিচের উপ-মোট হলো এই সময়ে তার মোট খরচ।"),
        new("Days on which an animal had no cost are left out to keep the report short.",
            "যে দিনে কোনো খরচ হয়নি সেই দিনগুলো প্রতিবেদন ছোট রাখতে বাদ দেওয়া হয়েছে।"),
        new("Feed is what was actually fed that day; vet & medicine is booked on the day of treatment or vaccination.",
            "খাদ্য খরচ সেদিন আসলে যা খাওয়ানো হয়েছে; চিকিৎসা ও ওষুধ খরচ চিকিৎসা বা টিকার দিনে ধরা হয়েছে।"),
        new("Labour and overhead are shared among animals by the days each was on the farm, then spread evenly over those days.",
            "শ্রম ও পরোক্ষ খরচ প্রতিটি পশুর খামারে থাকার দিন অনুযায়ী ভাগ করে সেই দিনগুলোতে সমানভাবে ছড়ানো হয়েছে।"),
        new("A sold or dead animal is marked next to its tag and its costs stop the day before it left. Purchase prices are not included.",
            "বিক্রি বা মৃত পশুর ট্যাগের পাশে তা লেখা আছে, এবং খামার ছাড়ার আগের দিন পর্যন্ত খরচ ধরা হয়েছে। ক্রয়মূল্য ধরা হয়নি।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
        ReportParameter.Batch(name: "batchId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<AnimalDailyCostByAnimalReportRow>> Columns =>
    [
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 24f),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Number("feedKg", new LocalizedText("Feed fed (kg)", "খাওয়ানো খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য (টাকা)"), r => r.FeedBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("vet", new LocalizedText("Vet & medicine (BDT)", "চিকিৎসা ও ওষুধ (টাকা)"), r => r.VetBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("labour", new LocalizedText("Labour (BDT)", "শ্রম (টাকা)"), r => r.LabourBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("overhead", new LocalizedText("Overhead (BDT)", "পরোক্ষ খরচ (টাকা)"), r => r.OverheadBdt, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalDailyCostByAnimalReportRow>.Money("total", new LocalizedText("Total for the day (BDT)", "দিনের মোট (টাকা)"), r => r.TotalBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<AnimalDailyCostByAnimalReportRow>? Group =>
        ReportGroup<AnimalDailyCostByAnimalReportRow>.By(r => r.GroupKey, r => r.GroupLabel);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var scope = await _scope.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{scope.Label} · {AnimalCostReportText.Period(scope.Period)}";
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

        var bn = context.Language == ReportLanguage.Bn;
        var unknown = bn ? "রেকর্ডে নেই এমন পশু" : "Animal no longer on record";

        string TagOf(Guid id) => animalsById.TryGetValue(id, out var a) ? AnimalLabels.Tag(a) : "~";
        string LabelOf(Guid id)
        {
            if (!animalsById.TryGetValue(id, out var a))
            {
                return (bn ? "পশু: " : "Animal: ") + unknown;
            }

            var parts = new List<string>
            {
                (bn ? "পশু: " : "Animal: ") + AnimalLabels.Tag(a),
                breeds.GetValueOrDefault(a.BreedId) ?? AnimalCostReportText.Species(a.Species, context.Language),
                AnimalCostReportText.Sex(a.Sex, context.Language),
            };
            if (a.BatchId is { } b && batchNames.TryGetValue(b, out var name))
            {
                parts.Add((bn ? "ব্যাচ " : "Batch ") + name);
            }

            return string.Join(" · ", parts);
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
