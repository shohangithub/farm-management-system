using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <summary>
/// One ration given to one animal on one day, with its feeding status.
/// </summary>
/// <param name="Status">The feeding status in plain words ("Fed (confirmed)").</param>
/// <param name="WeightKg">Weight used for the ration that day; null when none was recorded.</param>
/// <param name="DryMatterKg">Null when the recipe has no dry-matter value.</param>
/// <param name="CrudeProteinGrams">Null when the recipe has no protein value.</param>
/// <param name="Notes">Why the amount was changed, for an adjusted feeding.</param>
public sealed record DailyFeedingWorkflowReportRow(
    DateOnly Date,
    string AnimalTag,
    string Species,
    string Location,
    string RuleSetName,
    string FormulaName,
    string Status,
    decimal? WeightKg,
    decimal FeedKg,
    decimal DryMatterPercent,
    decimal? DryMatterKg,
    decimal CrudeProteinPercent,
    decimal? CrudeProteinGrams,
    decimal UnitCostBdt,
    decimal TotalCostBdt,
    string Notes);

/// <summary>
/// Report F3 — Daily feeding record by animal: what each animal was given each day, whether that
/// feeding was confirmed, and the dry matter, protein and cost in it.
/// </summary>
public sealed class DailyFeedingWorkflowReportDefinition : ReportDefinition<DailyFeedingWorkflowReportRow>
{
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IDailyFeedingEntryRepository _entries;
    private readonly IAnimalRepository _animals;
    private readonly IFeedFormulaRepository _formulas;
    private readonly IFeedingRuleSetRepository _ruleSets;
    private readonly IAnimalFeedingPlanRepository _plans;
    private readonly IShedRepository _sheds;

    public DailyFeedingWorkflowReportDefinition(
        IAnimalFeedAllocationRepository allocations,
        IDailyFeedingEntryRepository entries,
        IAnimalRepository animals,
        IFeedFormulaRepository formulas,
        IFeedingRuleSetRepository ruleSets,
        IAnimalFeedingPlanRepository plans,
        IShedRepository sheds)
    {
        _allocations = allocations;
        _entries = entries;
        _animals = animals;
        _formulas = formulas;
        _ruleSets = ruleSets;
        _plans = plans;
        _sheds = sheds;
    }

    public override string Key => "feeding.daily-workflow";

    public override LocalizedText Title => new(
        "Daily Feeding Record by Animal",
        "পশুভিত্তিক দৈনিক খাওয়ানোর রেকর্ড");

    public override LocalizedText Description => new(
        "What each animal was given each day, whether the feeding was confirmed, and the dry matter, protein and cost in it.",
        "প্রতিদিন প্রতিটি পশুকে কী দেওয়া হয়েছে, খাওয়ানো নিশ্চিত হয়েছে কি না, এবং তাতে কত শুষ্ক পদার্থ, প্রোটিন ও খরচ।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(@default: "current-month"),
        ReportParameter.Select(
            "status",
            new LocalizedText("Feeding status", "খাওয়ানোর অবস্থা"),
            [
                new ReportSelectOption("all", new LocalizedText("All statuses", "সকল অবস্থা")),
                new ReportSelectOption("Confirmed", new LocalizedText("Fed (confirmed)", "খাওয়ানো হয়েছে (নিশ্চিত)")),
                new ReportSelectOption("Pending", new LocalizedText("Not yet confirmed", "এখনো নিশ্চিত হয়নি")),
                new ReportSelectOption("Adjusted", new LocalizedText("Fed (amount changed)", "খাওয়ানো হয়েছে (পরিমাণ পরিবর্তিত)")),
                new ReportSelectOption("Skipped", new LocalizedText("Skipped", "বাদ দেওয়া হয়েছে")),
            ],
            @default: "all"),
        ReportParameter.Animal(required: false),
        ReportParameter.Batch(required: false),
        ReportParameter.Shed(required: false)
    ];

    // Tag, species and shed are in the group band above each animal's rows, so they are not repeated
    // on every line; dry-matter % and protein % belong to the recipe and are folded into the kg/g.
    public override IReadOnlyList<ReportColumn<DailyFeedingWorkflowReportRow>> Columns =>
    [
        ReportColumn<DailyFeedingWorkflowReportRow>.Date(
            "date",
            new LocalizedText("Date", "তারিখ"),
            r => r.Date,
            widthMm: 22f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "rule",
            new LocalizedText("Feed chart", "খাদ্য তালিকা"),
            r => r.RuleSetName,
            width: 1.8f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "formula",
            new LocalizedText("Recipe (feed mix)", "রেসিপি (খাদ্য মিশ্রণ)"),
            r => r.FormulaName,
            width: 2.0f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "status",
            new LocalizedText("Feeding status", "খাওয়ানোর অবস্থা"),
            r => r.Status,
            width: 1.6f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "weight",
            new LocalizedText("Weight (kg)", "ওজন (কেজি)"),
            r => r.WeightKg,
            decimals: 1,
            widthMm: 18f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "feed",
            new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"),
            r => r.FeedKg,
            decimals: 2,
            widthMm: 18f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "dmKg",
            new LocalizedText("Dry matter (kg)", "শুষ্ক পদার্থ (কেজি)"),
            r => r.DryMatterKg,
            decimals: 2,
            widthMm: 20f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "cpGrams",
            new LocalizedText("Protein (g)", "প্রোটিন (গ্রাম)"),
            r => r.CrudeProteinGrams,
            decimals: 0,
            widthMm: 18f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Money(
            "rate",
            new LocalizedText("Rate (BDT/kg)", "দর (টাকা/কেজি)"),
            r => r.UnitCostBdt,
            widthMm: 20f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.Money(
            "cost",
            new LocalizedText("Cost (BDT)", "খরচ (টাকা)"),
            r => r.TotalCostBdt,
            widthMm: 22f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "notes",
            new LocalizedText("Reason for change", "পরিবর্তনের কারণ"),
            r => r.Notes,
            width: 1.6f),
    ];

    public override ReportGroup<DailyFeedingWorkflowReportRow>? Group =>
        ReportGroup<DailyFeedingWorkflowReportRow>.ByLocalized(
            r => r.AnimalTag,
            r => new LocalizedText(
                $"Animal: {r.AnimalTag} · {r.Species} · Shed: {r.Location}",
                $"পশু: {r.AnimalTag} · {r.Species} · শেড: {r.Location}"));

    public override LocalizedText? GrandTotalLabel => new("Total for all animals shown", "দেখানো সব পশুর মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Rows are grouped by animal; each row is one ration given on one day, with a sub-total for each animal.",
            "সারিগুলো পশু অনুযায়ী সাজানো; প্রতিটি সারি একদিনের একটি রেশন, প্রতিটি পশুর নিচে উপ-মোট আছে।"),
        new("\"Not yet confirmed\" means the feeding was planned but nobody has confirmed it was given; only confirmed or changed feedings count as fed.",
            "\"এখনো নিশ্চিত হয়নি\" মানে খাওয়ানোর পরিকল্পনা ছিল কিন্তু কেউ নিশ্চিত করেনি; শুধু নিশ্চিত বা পরিবর্তিত খাওয়ানোই খাওয়ানো হিসেবে ধরা হয়।"),
        new("Dry matter (kg) = feed × the recipe's dry-matter %; Protein (g) = feed × the recipe's protein %. Blank means the recipe has no nutrition values.",
            "শুষ্ক পদার্থ (কেজি) = খাদ্য × রেসিপির শুষ্ক পদার্থ %; প্রোটিন (গ্রাম) = খাদ্য × রেসিপির প্রোটিন %। ফাঁকা মানে রেসিপিতে পুষ্টির তথ্য নেই।"),
        new("Cost (BDT) = Feed (kg) × Rate (BDT/kg).",
            "খরচ (টাকা) = খাদ্য (কেজি) × দর (টাকা/কেজি)।"),
        new("Sold or dead animals show the date they left beside the tag, and appear only for days they were on the farm or were actually fed.",
            "বিক্রি হওয়া বা মৃত পশুর ট্যাগের পাশে খামার ছাড়ার তারিখ থাকে; শুধু খামারে থাকা বা সত্যিই খাওয়ানো দিনগুলো দেখানো হয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var language = context.Language;
        var parts = new List<string> { FeedReportText.Period(context.Range("period")) };

        var shedId = context.IdOrNull("shedId");
        if (shedId.HasValue)
        {
            var shed = await _sheds.GetByIdAsync(context.TenantId, shedId.Value, cancellationToken).ConfigureAwait(false);
            if (shed is not null)
            {
                parts.Add(new LocalizedText("Shed: ", "শেড: ").For(language) + ShedLabel(shed));
            }
        }

        var animalId = context.IdOrNull("animalId");
        if (animalId.HasValue)
        {
            var animal = await _animals.GetByIdAsync(animalId.Value, cancellationToken).ConfigureAwait(false);
            if (animal is not null)
            {
                parts.Add(new LocalizedText("Tag ", "ট্যাগ ").For(language) + AnimalLabels.Tag(animal, context));
            }
        }

        var status = context.Text("status", "all");
        if (Enum.TryParse<DailyFeedingEntryStatus>(status, ignoreCase: true, out var parsed))
        {
            parts.Add(FeedReportText.FeedingStatus(parsed).For(language));
        }

        return string.Join(" · ", parts);
    }

    protected override async Task<IReadOnlyList<DailyFeedingWorkflowReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var period = context.Range("period");
        var statusFilter = context.Text("status", "all");
        var animalId = context.IdOrNull("animalId");
        var batchId = context.IdOrNull("batchId");
        var shedId = context.IdOrNull("shedId");
        var language = context.Language;

        var allocations = await _allocations
            .GetByFarmAsync(farmId, period.From, period.To, animalId, batchId, shedId, cancellationToken)
            .ConfigureAwait(false);

        if (allocations.Count == 0)
        {
            return [];
        }

        // Pre-load distinct animals in bulk
        var animalIds = allocations.Select(a => a.AnimalId).Distinct().ToList();
        var animalsList = await _animals.GetByIdsAsync(animalIds, cancellationToken).ConfigureAwait(false);
        var animalsMap = animalsList.ToDictionary(a => a.Id);

        // Pre-load distinct daily feeding entries in bulk to read workflow status
        var entryIds = allocations.Select(a => a.DailyFeedingEntryId).Distinct().ToList();
        var entriesList = await _entries.GetByIdsAsync(entryIds, cancellationToken).ConfigureAwait(false);
        var entriesMap = entriesList.ToDictionary(e => e.Id);

        // Pre-load distinct formulas for nutrition profile
        var formulaIds = allocations.Select(a => a.FormulaId).Distinct().ToList();
        var formulasMap = new Dictionary<Guid, FeedFormula?>();
        foreach (var fid in formulaIds)
        {
            formulasMap[fid] = await _formulas.GetByIdAsync(fid, cancellationToken).ConfigureAwait(false);
        }

        // Pre-load plans & rule sets
        var planIds = allocations.Select(a => a.FeedingPlanId).Distinct().ToList();
        var plansList = await _plans.GetByIdsAsync(planIds, cancellationToken).ConfigureAwait(false);
        var plansMap = plansList.ToDictionary(p => p.Id);

        var ruleSetIds = plansList.Select(p => p.FeedingRuleSetId).Distinct().ToList();
        var ruleSetsMap = new Dictionary<Guid, FeedingRuleSet?>();
        foreach (var rid in ruleSetIds)
        {
            ruleSetsMap[rid] = await _ruleSets.GetByIdAsync(rid, cancellationToken).ConfigureAwait(false);
        }

        // Pre-load sheds for human-readable location
        var shedsList = await _sheds.GetAllByFarmAsync(context.TenantId, farmId, cancellationToken).ConfigureAwait(false);
        var shedsMap = shedsList.ToDictionary(s => s.Id);

        var notRecorded = FeedReportText.NotRecorded.For(language);
        var noShed = new LocalizedText("none", "নেই").For(language);
        var unknownAnimal = new LocalizedText("Unknown animal", "অজানা পশু").For(language);

        var rows = new List<DailyFeedingWorkflowReportRow>(allocations.Count);

        foreach (var a in allocations)
        {
            if (!entriesMap.TryGetValue(a.DailyFeedingEntryId, out var entry) || entry == null)
            {
                // Skip orphaned allocations that no longer map to an active daily feeding entry
                continue;
            }

            var statusStr = entry.Status.ToString();

            if (!string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(statusFilter, statusStr, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            animalsMap.TryGetValue(a.AnimalId, out var animal);

            // Not on the farm that day (sold/died before or on it) and not actually fed: not part
            // of the day's workflow.
            if (animal is not null && !animal.IsOnFarmOn(a.EntryDate)
                && entry.Status is not (DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted))
            {
                continue;
            }

            formulasMap.TryGetValue(a.FormulaId, out var formula);
            plansMap.TryGetValue(a.FeedingPlanId, out var plan);

            FeedingRuleSet? ruleSet = null;
            if (plan != null)
            {
                ruleSetsMap.TryGetValue(plan.FeedingRuleSetId, out ruleSet);
            }

            string tag = animal is null ? unknownAnimal : AnimalLabels.Tag(animal, context);
            string species = animal is null ? notRecorded : FeedReportText.Species(animal.Species).For(language);

            string location = noShed;
            if (a.ShedId is { } targetShedId && shedsMap.TryGetValue(targetShedId, out var shed))
            {
                location = ShedLabel(shed);
            }

            string ruleName = ruleSet?.Name ?? notRecorded;
            string formulaName = formula?.Title ?? notRecorded;

            // Nutrition values from FeedFormula. A recipe without them gives a blank, not a 0 that
            // reads like "no protein in this feed".
            decimal dmPct = formula?.NutritionalProfile.DryMatterPercentage ?? 0m;
            decimal cpPct = formula?.NutritionalProfile.CrudeProteinPercentage ?? 0m;

            decimal feedKg = a.AllocatedKg;
            decimal? dmKg = dmPct > 0 ? Math.Round(feedKg * (dmPct / 100m), 2) : null;
            decimal? cpGrams = cpPct > 0 ? Math.Round(feedKg * (cpPct / 100m) * 1000m, 0) : null;

            decimal? weight = a.WeightAtAllocationKg > 0 ? a.WeightAtAllocationKg : animal?.LatestWeightKg;

            rows.Add(new DailyFeedingWorkflowReportRow(
                Date: a.EntryDate,
                AnimalTag: tag,
                Species: species,
                Location: location,
                RuleSetName: ruleName,
                FormulaName: formulaName,
                Status: FeedReportText.FeedingStatus(entry.Status).For(language),
                WeightKg: weight is > 0m ? weight : null,
                FeedKg: feedKg,
                DryMatterPercent: dmPct,
                DryMatterKg: dmKg,
                CrudeProteinPercent: cpPct,
                CrudeProteinGrams: cpGrams,
                UnitCostBdt: a.UnitCostBdtPerKg,
                TotalCostBdt: a.AllocatedCostBdt,
                Notes: entry.AdjustmentReason ?? string.Empty));
        }

        return rows
            .OrderBy(r => r.AnimalTag, StringComparer.Ordinal)
            .ThenBy(r => r.Date)
            .ToList();
    }

    /// <summary>"A", or "A – Fattening shed" when the shed has a name as well as a number.</summary>
    private static string ShedLabel(Shed shed) =>
        string.IsNullOrWhiteSpace(shed.ShedName) || string.Equals(shed.ShedName, shed.ShedNumber, StringComparison.OrdinalIgnoreCase)
            ? shed.ShedNumber
            : $"{shed.ShedNumber} – {shed.ShedName}";
}
