using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Livestock.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <summary>One day's feed for one animal, with the rule and recipe that produced it.</summary>
public sealed record AnimalFeedingReportRow(
    DateOnly Date,
    string RuleSet,
    string Recipe,
    decimal AllocatedKg,
    decimal UnitCostBdt,
    decimal CostBdt,
    string Basis,
    int HeadCount);

/// <summary>
/// Report A1 — date-wise feed quantity, cost, feeding rule and recipe for a single animal,
/// sub-totalled by month.
/// </summary>
/// <remarks>
/// This is the report GAP-1 blocked. It reads <see cref="AnimalFeedAllocation"/> rather than
/// <c>DailyFeedingEntry</c>, which is why it now works for animals fed under a batch, shed or pen
/// plan as well as those on an individual plan. The Basis column shows whether a row was the
/// animal's own ration or a computed share of a group's feed, so the reader can see which numbers
/// are measured and which are apportioned.
/// </remarks>
public sealed class AnimalFeedingReportDefinition : ReportDefinition<AnimalFeedingReportRow>
{
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IFeedFormulaRepository _formulas;
    private readonly IFeedingRuleSetRepository _ruleSets;
    private readonly IAnimalFeedingPlanRepository _plans;

    public AnimalFeedingReportDefinition(
        IAnimalFeedAllocationRepository allocations,
        IFeedFormulaRepository formulas,
        IFeedingRuleSetRepository ruleSets,
        IAnimalFeedingPlanRepository plans)
    {
        _allocations = allocations;
        _formulas = formulas;
        _ruleSets = ruleSets;
        _plans = plans;
    }

    public override string Key => "feeding.animal-feeding";

    public override LocalizedText Title => new("Animal Feeding Report", "পশুর খাদ্য প্রতিবেদন");

    public override LocalizedText Description => new(
        "Date-wise feed quantity, cost, feeding rule and recipe for one animal.",
        "একটি পশুর দৈনিক খাদ্যের পরিমাণ, খরচ, খাদ্য নিয়ম ও রেসিপি।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalFeedingReportRow>> Columns =>
    [
        ReportColumn<AnimalFeedingReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<AnimalFeedingReportRow>.Text("ruleSet", new LocalizedText("Feeding Rule", "খাদ্য নিয়ম"), r => r.RuleSet, width: 2.2f),
        ReportColumn<AnimalFeedingReportRow>.Text("recipe", new LocalizedText("Recipe", "রেসিপি"), r => r.Recipe, width: 2.2f),
        ReportColumn<AnimalFeedingReportRow>.Number("kg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.AllocatedKg, decimals: 3, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalFeedingReportRow>.Money("rate", new LocalizedText("Rate BDT/kg", "দর টাকা/কেজি"), r => r.UnitCostBdt, widthMm: 22f, aggregate: ReportAggregate.None),
        ReportColumn<AnimalFeedingReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, widthMm: 24f),
        ReportColumn<AnimalFeedingReportRow>.Text("basis", new LocalizedText("Basis", "ভিত্তি"), r => r.Basis, width: 26f, relative: false),
        ReportColumn<AnimalFeedingReportRow>.WholeNumber("head", new LocalizedText("Head", "সংখ্যা"), r => r.HeadCount, widthMm: 14f),
    ];

    public override ReportGroup<AnimalFeedingReportRow>? Group =>
        ReportGroup<AnimalFeedingReportRow>.By(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => r.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await context
            .Send(new GetAnimalByIdQuery(context.Id("animalId")), cancellationToken)
            .ConfigureAwait(false);

        if (animal is null)
        {
            return null;
        }

        var weight = animal.LatestWeightKg is { } kg
            ? $" · {kg.ToString("N1", CultureInfo.InvariantCulture)} kg"
            : string.Empty;

        return $"Tag {animal.TagId} · {animal.Species}{weight} · {animal.Status}";
    }

    protected override async Task<IReadOnlyList<AnimalFeedingReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var period = context.Range("period");

        var allocations = await _allocations
            .GetByAnimalAsync(animalId, period.From, period.To, cancellationToken)
            .ConfigureAwait(false);

        if (allocations.Count == 0)
        {
            return [];
        }

        // One lookup per *distinct* formula and rule set, not per row: a year of daily feed is
        // ~365 rows over a handful of recipes.
        var formulaNames = await ResolveFormulaNamesAsync(allocations, cancellationToken).ConfigureAwait(false);
        var ruleSetNames = await ResolveRuleSetNamesAsync(allocations, cancellationToken).ConfigureAwait(false);

        return allocations
            .Select(a => new AnimalFeedingReportRow(
                a.EntryDate,
                ruleSetNames.GetValueOrDefault(a.FeedingPlanId, "—"),
                formulaNames.GetValueOrDefault(a.FormulaId, "—"),
                a.AllocatedKg,
                a.UnitCostBdtPerKg,
                a.AllocatedCostBdt,
                DescribeBasis(a),
                a.HeadCountAtAllocation))
            .ToList();
    }

    /// <summary>
    /// Says plainly whether a figure was this animal's own ration or a share of a group's feed,
    /// and by what rule — an apportioned cost should never look like a measured one.
    /// </summary>
    private static string DescribeBasis(AnimalFeedAllocation a)
    {
        if (a.Origin == FeedAllocationOrigin.IndividualPlan)
        {
            return "Individual";
        }

        var method = a.Method switch
        {
            Domain.BusinessRules.FeedAllocationMethod.MetabolicWeight => "W^0.75",
            Domain.BusinessRules.FeedAllocationMethod.LiveWeight => "by weight",
            _ => "equal",
        };

        return a.IsBackfilled ? $"Group ({method}, backfilled)" : $"Group ({method})";
    }

    private async Task<Dictionary<Guid, string>> ResolveFormulaNamesAsync(
        IReadOnlyList<AnimalFeedAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<Guid, string>();

        foreach (var formulaId in allocations.Select(a => a.FormulaId).Distinct())
        {
            var formula = await _formulas.GetByIdAsync(formulaId, cancellationToken).ConfigureAwait(false);
            if (formula is not null)
            {
                names[formulaId] = formula.Title;
            }
        }

        return names;
    }

    private async Task<Dictionary<Guid, string>> ResolveRuleSetNamesAsync(
        IReadOnlyList<AnimalFeedAllocation> allocations,
        CancellationToken cancellationToken)
    {
        var byPlan = new Dictionary<Guid, string>();

        var plans = await _plans
            .GetByIdsAsync(allocations.Select(a => a.FeedingPlanId).Distinct(), cancellationToken)
            .ConfigureAwait(false);

        foreach (var plan in plans)
        {
            var ruleSet = await _ruleSets.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken).ConfigureAwait(false);
            byPlan[plan.Id] = ruleSet?.Name ?? "—";
        }

        return byPlan;
    }
}
