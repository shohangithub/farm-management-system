using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <summary>
/// Row model for the Daily Feeding Entries & Nutrition Workflow SAP Report.
/// </summary>
public sealed record DailyFeedingWorkflowReportRow(
    DateOnly Date,
    string AnimalTag,
    string Species,
    string Location,
    string RuleSetName,
    string FormulaName,
    string Status,
    decimal WeightKg,
    decimal FeedKg,
    decimal DryMatterPercent,
    decimal DryMatterKg,
    decimal CrudeProteinPercent,
    decimal CrudeProteinGrams,
    decimal UnitCostBdt,
    decimal TotalCostBdt,
    string Notes);

/// <summary>
/// Report F3 — Daily Feeding Entries & Workflow Report.
/// Displays animal-wise chronological feed intake, nutritional breakdown (Dry Matter and Crude Protein),
/// unit rate, financial cost, and operational workflow status (Pending, Confirmed, Adjusted, Skipped).
/// Formatted according to SAP enterprise reporting standards.
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
        "Daily Feeding Entries & Nutrition Workflow Report",
        "দৈনিক খাদ্য গ্রহণ, পুষ্টি ও কর্মপ্রবাহ প্রতিবেদন");

    public override LocalizedText Description => new(
        "Animal-wise daily feeding history with workflow status, ration quantities, nutrient intake (Dry Matter & Crude Protein), unit rates, and cost analytics.",
        "প্রতিটি পশুর দৈনিক খাদ্য গ্রহণের ইতিহাস, অনুমোদন অবস্থা, পুষ্টির পরিমাণ (ডিএম ও প্রোটিন), একক দর ও খাদ্য ব্যয়ের বিস্তারিত প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(@default: "current-month"),
        ReportParameter.Select(
            "status",
            new LocalizedText("Workflow Status", "কর্মপ্রবাহ অবস্থা"),
            [
                new ReportSelectOption("all", new LocalizedText("All Statuses", "সকল অবস্থা")),
                new ReportSelectOption("Confirmed", new LocalizedText("Confirmed", "অনুমোদিত")),
                new ReportSelectOption("Pending", new LocalizedText("Pending", "অপেক্ষমান")),
                new ReportSelectOption("Adjusted", new LocalizedText("Adjusted", "সংশোধিত")),
                new ReportSelectOption("Skipped", new LocalizedText("Skipped", "বাদ দেওয়া")),
            ],
            @default: "all"),
        ReportParameter.Animal(required: false),
        ReportParameter.Batch(required: false),
        ReportParameter.Shed(required: false)
    ];

    public override IReadOnlyList<ReportColumn<DailyFeedingWorkflowReportRow>> Columns =>
    [
        ReportColumn<DailyFeedingWorkflowReportRow>.Date(
            "date",
            new LocalizedText("Date", "তারিখ"),
            r => r.Date,
            widthMm: 22f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "tag",
            new LocalizedText("Animal Tag", "ট্যাগ নম্বর"),
            r => r.AnimalTag,
            width: 1.8f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "species",
            new LocalizedText("Species", "প্রজাতি"),
            r => r.Species,
            width: 1.4f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "location",
            new LocalizedText("Location", "অবস্থান"),
            r => r.Location,
            width: 1.6f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "rule",
            new LocalizedText("Feeding Rule", "খাদ্য নিয়ম"),
            r => r.RuleSetName,
            width: 1.8f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "formula",
            new LocalizedText("Recipe / Formula", "রেসিপি / ফর্মুলা"),
            r => r.FormulaName,
            width: 2.0f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Text(
            "status",
            new LocalizedText("Status", "অবস্থা"),
            r => r.Status,
            width: 1.3f),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "weight",
            new LocalizedText("Wt (kg)", "ওজন"),
            r => r.WeightKg,
            decimals: 1,
            widthMm: 16f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "feed",
            new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"),
            r => r.FeedKg,
            decimals: 2,
            widthMm: 18f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "dmPct",
            new LocalizedText("DM %", "ডিএম %"),
            r => r.DryMatterPercent,
            decimals: 1,
            widthMm: 14f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "dmKg",
            new LocalizedText("DM (kg)", "ডিএম (কেজি)"),
            r => r.DryMatterKg,
            decimals: 2,
            widthMm: 17f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Number(
            "cpPct",
            new LocalizedText("CP %", "সিপি %"),
            r => r.CrudeProteinPercent,
            decimals: 1,
            widthMm: 14f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.WholeNumber(
            "cpGrams",
            new LocalizedText("CP (g)", "সিপি (গ্রাম)"),
            r => (int)Math.Round(r.CrudeProteinGrams),
            widthMm: 16f,
            aggregate: ReportAggregate.Sum),

        ReportColumn<DailyFeedingWorkflowReportRow>.Money(
            "rate",
            new LocalizedText("Rate (BDT)", "দর (টাকা)"),
            r => r.UnitCostBdt,
            widthMm: 18f,
            aggregate: ReportAggregate.None),

        ReportColumn<DailyFeedingWorkflowReportRow>.Money(
            "cost",
            new LocalizedText("Cost (BDT)", "খরচ (টাকা)"),
            r => r.TotalCostBdt,
            widthMm: 20f,
            aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<DailyFeedingWorkflowReportRow>? Group =>
        ReportGroup<DailyFeedingWorkflowReportRow>.By(
            r => r.AnimalTag,
            r => $"Animal: {r.AnimalTag} · {r.Species} · {r.Location}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = context.Range("period");
        return $"Herd Daily Feed Consumption & Nutrition Workflow · {period.From:dd MMM yyyy} to {period.To:dd MMM yyyy}";
    }

    protected override async Task<IReadOnlyList<DailyFeedingWorkflowReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var period = context.Range("period");
        var statusFilter = context.Text("status", "all");
        var animalId = context.IdOrNull("animalId");
        var batchId = context.IdOrNull("batchId");
        var shedId = context.IdOrNull("shedId");

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
            formulasMap.TryGetValue(a.FormulaId, out var formula);
            plansMap.TryGetValue(a.FeedingPlanId, out var plan);

            FeedingRuleSet? ruleSet = null;
            if (plan != null)
            {
                ruleSetsMap.TryGetValue(plan.FeedingRuleSetId, out ruleSet);
            }

            string tag = animal?.Tag.TagId ?? "Tag —";
            string species = animal?.Species.ToString() ?? "Cattle";

            Guid? targetShedId = a.ShedId;
            string location = "General Herd";
            if (targetShedId.HasValue && shedsMap.TryGetValue(targetShedId.Value, out var shed))
            {
                location = shed.ShedNumber;
            }

            string ruleName = ruleSet?.Name ?? "Standard Rule";
            string formulaName = formula?.Title ?? "Daily Mixed Ration";

            // Nutrition values from FeedFormula
            decimal dmPct = formula?.NutritionalProfile.DryMatterPercentage ?? 0m;
            decimal cpPct = formula?.NutritionalProfile.CrudeProteinPercentage ?? 0m;

            decimal feedKg = a.AllocatedKg;
            decimal dmKg = dmPct > 0 ? Math.Round(feedKg * (dmPct / 100m), 2) : 0m;
            decimal cpGrams = cpPct > 0 ? Math.Round(feedKg * (cpPct / 100m) * 1000m, 0) : 0m;

            decimal weight = a.WeightAtAllocationKg > 0 ? a.WeightAtAllocationKg : (animal?.LatestWeightKg ?? 0m);

            rows.Add(new DailyFeedingWorkflowReportRow(
                Date: a.EntryDate,
                AnimalTag: tag,
                Species: species,
                Location: location,
                RuleSetName: ruleName,
                FormulaName: formulaName,
                Status: statusStr,
                WeightKg: weight,
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
            .OrderBy(r => r.AnimalTag)
            .ThenBy(r => r.Date)
            .ToList();
    }

    private static async Task<Guid> ResolveFarmIdAsync(ReportContext context, CancellationToken cancellationToken)
    {
        if (context.IdOrNull("farmId") is { } id && id != Guid.Empty)
            return id;
        if (context.AssignedFarmIds is { Count: > 0 } assigned && assigned[0] != Guid.Empty)
            return assigned[0];

        var farms = await context.Send(new GetAllFarmsQuery(), cancellationToken).ConfigureAwait(false);
        return farms.Count > 0 ? farms[0].Id : Guid.Empty;
    }
}
