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
    decimal? WeightKg,
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
        ReportParameter.Farm(required: true),
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
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("ruleSetName", new LocalizedText("Feed Chart / Ration", "খাদ্য তালিকা"), r => r.RuleSetName, width: 2.6f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Text("formulaName", new LocalizedText("Formula / Recipe", "রেসিপি / ফর্মুলা"), r => r.FormulaName, width: 2.6f),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("concentrate", new LocalizedText("Concentrate (kg/day)", "দানাদার (কেজি/দিন)"), r => r.ConcentrateKgPerDay, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("roughage", new LocalizedText("Roughage (kg/day)", "আঁশজাত (কেজি/দিন)"), r => r.RoughageKgPerDay, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Number("dailyFeed", new LocalizedText("Total feed (kg/day)", "মোট খাদ্য (কেজি/দিন)"), r => r.ExpectedDailyFeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("rate", new LocalizedText("Rate (BDT/kg)", "দর (টাকা/কেজি)"), r => r.EstimatedCostPerKgBdt, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.None),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("dailyCost", new LocalizedText("Daily cost (BDT)", "দৈনিক খরচ (টাকা)"), r => r.EstimatedDailyCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<ActiveFeedingPlansCostReportRow>.Money("monthlyCost", new LocalizedText("30-day cost (BDT)", "৩০ দিনের খরচ (টাকা)"), r => r.EstimatedMonthlyCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
    ];

    // One block per cow: its plans underneath, and the cow's own daily total as the sub-total.
    public override ReportGroup<ActiveFeedingPlansCostReportRow>? Group =>
        ReportGroup<ActiveFeedingPlansCostReportRow>.ByLocalized(
            r => r.AnimalTag,
            r => new LocalizedText(AnimalHeading(r, "kg"), AnimalHeading(r, "কেজি")));

    private static string AnimalHeading(ActiveFeedingPlansCostReportRow r, string kg)
    {
        var parts = new List<string> { r.AnimalTag };
        if (!string.IsNullOrWhiteSpace(r.Species)) parts.Add(r.Species);
        if (r.WeightKg is { } w) parts.Add($"{w:0.#} {kg}");
        return string.Join(" · ", parts);
    }

    public override LocalizedText? GrandTotalLabel => new("Whole herd", "পুরো পাল");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each block is one animal (tag · species · weight); each row under it is one of its feeding plans as it stands today, and the sub-total is that animal's whole day.",
            "প্রতিটি ভাগ একটি পশু (ট্যাগ · প্রজাতি · ওজন); নিচের প্রতিটি সারি তার একটি আজকের খাদ্য পরিকল্পনা, আর উপ-মোট হলো ওই পশুর সারাদিনের খাদ্য ও খরচ।"),
        new("Concentrate is grain/mixed feed, roughage is grass and straw; total feed is the planned amount per day.",
            "দানাদার হলো দানা/মিশ্র খাদ্য, আঁশজাত হলো ঘাস ও খড়; মোট খাদ্য হলো দৈনিক পরিকল্পিত পরিমাণ।"),
        new("Daily cost = concentrate (kg/day) × rate (BDT/kg) at today's recipe price; roughage is not costed here.",
            "দৈনিক খরচ = দানাদার (কেজি/দিন) × দর (টাকা/কেজি), আজকের রেসিপির দামে; আঁশজাত খাদ্যের খরচ এখানে ধরা হয়নি।"),
        new("30-day cost = daily cost × 30. This is a forecast from the plan, not what was actually fed.",
            "৩০ দিনের খরচ = দৈনিক খরচ × ৩০। এটি পরিকল্পনা থেকে পূর্বাভাস, প্রকৃত খাওয়ানো নয়।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var which = context.Text("status", "Active") switch
        {
            var s when string.Equals(s, "all", StringComparison.OrdinalIgnoreCase) => new LocalizedText("All feeding plans", "সকল খাদ্য পরিকল্পনা"),
            var s when string.Equals(s, "Paused", StringComparison.OrdinalIgnoreCase) => new LocalizedText("Paused feeding plans", "স্থগিত খাদ্য পরিকল্পনা"),
            _ => new LocalizedText("Active feeding plans", "সক্রিয় খাদ্য পরিকল্পনা"),
        };
        var asOf = new LocalizedText("as of ", "তারিখ ").For(context.Language) + FeedReportText.Day(context.Today);

        return Task.FromResult<string?>($"{which.For(context.Language)} · {asOf}");
    }

    protected override async Task<IReadOnlyList<ActiveFeedingPlansCostReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
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
                Species: FeedReportText.Species(p.AnimalSpecies).For(context.Language),
                WeightKg: p.AnimalWeightKg is > 0m ? p.AnimalWeightKg : null,
                RuleSetName: p.RuleSetName,
                FormulaName: string.IsNullOrWhiteSpace(p.FormulaName) ? "Standard Ration" : p.FormulaName,
                ConcentrateKgPerDay: p.ConcentrateKgPerDay,
                RoughageKgPerDay: p.RoughageKgPerDay,
                ExpectedDailyFeedKg: p.ExpectedDailyFeedKg,
                EstimatedCostPerKgBdt: p.EstimatedCostPerKgBdt,
                EstimatedDailyCostBdt: p.EstimatedDailyCostBdt,
                EstimatedMonthlyCostBdt: decimal.Round(p.EstimatedDailyCostBdt * 30m, 2, MidpointRounding.AwayFromZero)))
            .OrderBy(r => r.AnimalTag, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.RuleSetName)
            .ToList();
    }
}
