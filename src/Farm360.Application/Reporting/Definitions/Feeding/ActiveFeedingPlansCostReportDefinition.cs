using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Feeding.Queries.AnimalFeedingPlans;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Feeding;

public sealed record ActiveFeedingPlansCostReportRow(
    string AnimalTag,
    string Species,
    decimal WeightKg,
    string RuleSetName,
    string FormulaName,
    decimal ConcentrateKgPerDay,
    decimal RoughageKgPerDay,
    decimal ExpectedDailyFeedKg,
    decimal EstimatedCostPerKgBdt,
    decimal EstimatedDailyCostBdt,
    decimal EstimatedMonthlyCostBdt);

/// <summary>
/// Report F2 — Active feeding plans across the herd with ration breakdowns, concentrate vs
/// roughage splits, and daily and 30-day cost forecasts.
/// </summary>
public sealed class ActiveFeedingPlansCostReportDefinition : ReportDefinition<ActiveFeedingPlansCostReportRow>
{
    public override string Key => "feeding.plans-cost-projection";

    public override LocalizedText Title => new(
        "Active Feeding Plans & Cost Projection Report",
        "সক্রিয় খাদ্য পরিকল্পনা ও খরচ পূর্বাভাস প্রতিবেদন");

    public override LocalizedText Description => new(
        "Animal-wise ration breakdown with concentrate/roughage splits, daily feed volumes and monthly cost projections.",
        "পশুভিত্তিক খাদ্যের পরিমাণ, দানাদার ও আঁশজাত খাদ্য বিভাজন, দৈনিক খাদ্য ও মাসিক ব্যয়ের পূর্বাভাস।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Select(
            "status",
            new LocalizedText("Plan Status", "পরিকল্পনা অবস্থা"),
            [
                new ReportSelectOption("Active", new LocalizedText("Active Plans", "সক্রিয় পরিকল্পনা")),
                new ReportSelectOption("all", new LocalizedText("All Plans", "সকল পরিকল্পনা")),
                new ReportSelectOption("Paused", new LocalizedText("Paused Plans", "স্থগিত পরিকল্পনা")),
            ],
            @default: "Active"),
    ];

    public override IReadOnlyList<ReportColumn<ActiveFeedingPlansCostReportRow>> Columns =>
    [
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("animalTag", new LocalizedText("Animal Tag", "পশুর ট্যাগ"), r => r.AnimalTag, width: 2.0f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("species", new LocalizedText("Species", "প্রজাতি"), r => r.Species, width: 1.6f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, decimals: 1, widthMm: 20f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("ruleSetName", new LocalizedText("Rule Set", "খাদ্য নিয়ম"), r => r.RuleSetName, width: 2.2f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("formulaName", new LocalizedText("Formula / Recipe", "রেসিপি / ফর্মুলা"), r => r.FormulaName, width: 2.2f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("concentrate", new LocalizedText("Conc. (kg/d)", "দানাদার (কেজি/দিন)"), r => r.ConcentrateKgPerDay, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("roughage", new LocalizedText("Rough. (kg/d)", "আঁশজাত (কেজি/দিন)"), r => r.RoughageKgPerDay, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("dailyFeed", new LocalizedText("Total Feed (kg)", "মোট খাদ্য (কেজি)"), r => r.ExpectedDailyFeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("rate", new LocalizedText("Rate (BDT/kg)", "দর (টাকা/কেজি)"), r => r.EstimatedCostPerKgBdt, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.None),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("dailyCost", new LocalizedText("Daily Cost", "দৈনিক খরচ"), r => r.EstimatedDailyCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("monthlyCost", new LocalizedText("30-Day Cost", "৩০ দিনের খরচ"), r => r.EstimatedMonthlyCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<ActiveFeedingPlansCostReportRow>? Group =>
        ReportGroup<ActiveFeedingPlansCostReportRow>.By(
            r => r.RuleSetName,
            r => $"Rule Set: {r.RuleSetName}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return "Herd Active Nutrition Plans & Monthly Projections";
    }

    protected override async Task<IReadOnlyList<ActiveFeedingPlansCostReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var status = context.Text("status", "Active");

        var plans = await context
            .Send(new GetFeedingPlansQuery(farmId, status), cancellationToken)
            .ConfigureAwait(false);

        if (plans.Count == 0)
        {
            return [];
        }

        return plans
            .Select(p => new ActiveFeedingPlansCostReportRow(
                AnimalTag: p.AnimalTag,
                Species: p.AnimalSpecies ?? "Cattle",
                WeightKg: p.AnimalWeightKg ?? 0m,
                RuleSetName: p.RuleSetName,
                FormulaName: string.IsNullOrWhiteSpace(p.FormulaName) ? "Standard Ration" : p.FormulaName,
                ConcentrateKgPerDay: p.ConcentrateKgPerDay,
                RoughageKgPerDay: p.RoughageKgPerDay,
                ExpectedDailyFeedKg: p.ExpectedDailyFeedKg,
                EstimatedCostPerKgBdt: p.EstimatedCostPerKgBdt,
                EstimatedDailyCostBdt: p.EstimatedDailyCostBdt,
                EstimatedMonthlyCostBdt: decimal.Round(p.EstimatedDailyCostBdt * 30m, 2, MidpointRounding.AwayFromZero)))
            .OrderBy(r => r.RuleSetName)
            .ThenBy(r => r.AnimalTag)
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
