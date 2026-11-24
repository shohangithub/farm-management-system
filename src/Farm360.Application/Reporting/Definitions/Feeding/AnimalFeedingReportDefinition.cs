using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Livestock.DTOs;
using Farm360.Application.Livestock.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Livestock.Enums;

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
/// plan as well as those on an individual plan. The "How it was fed" column shows whether a row was
/// the animal's own ration or a computed share of a group's feed, so the reader can see which
/// numbers are measured and which are apportioned.
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
        "What one animal was fed each day — which feed chart and recipe, how many kg, and what it cost.",
        "একটি পশুকে প্রতিদিন কী খাওয়ানো হয়েছে — কোন খাদ্য তালিকা ও রেসিপি, কত কেজি এবং কত খরচ।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalFeedingReportRow>> Columns =>
    [
        // One date cell per day, spanning that day's rations (PDF and on-screen; Excel keeps every row filled for filtering).
        ReportColumn<AnimalFeedingReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, mergeRepeating: true),
        ReportColumn<AnimalFeedingReportRow>.Text("ruleSet", new LocalizedText("Feed chart", "খাদ্য তালিকা"), r => r.RuleSet, width: 2.0f),
        ReportColumn<AnimalFeedingReportRow>.Text("recipe", new LocalizedText("Recipe (feed mix)", "রেসিপি (খাদ্য মিশ্রণ)"), r => r.Recipe, width: 2.0f),
        ReportColumn<AnimalFeedingReportRow>.Text("basis", new LocalizedText("How it was fed", "কীভাবে খাওয়ানো হয়েছে"), r => r.Basis, width: 2.4f),
        ReportColumn<AnimalFeedingReportRow>.WholeNumber("head", new LocalizedText("Animals sharing (head)", "ভাগীদার পশু (টি)"), r => r.HeadCount, widthMm: 18f),
        ReportColumn<AnimalFeedingReportRow>.Number("kg", new LocalizedText("Feed (kg)", "খাদ্য (কেজি)"), r => r.AllocatedKg, decimals: 3, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalFeedingReportRow>.Money("rate", new LocalizedText("Rate (BDT/kg)", "দর (টাকা/কেজি)"), r => r.UnitCostBdt, widthMm: 22f, aggregate: ReportAggregate.None),
        ReportColumn<AnimalFeedingReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<AnimalFeedingReportRow>? Group =>
        ReportGroup<AnimalFeedingReportRow>.ByLocalized(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => FeedReportText.Month(r.Date));

    public override LocalizedText? GrandTotalLabel => new("Total for the period", "পুরো সময়ের মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one ration this animal received on one day; a day can have more than one row.",
            "প্রতিটি সারি হলো একদিনে এই পশুকে দেওয়া একটি রেশন; একই দিনে একাধিক সারি থাকতে পারে।"),
        new("\"How it was fed\" shows whether the feed was the animal's own ration or its share of a shed, batch or pen ration, divided among the animals sharing it.",
            "\"কীভাবে খাওয়ানো হয়েছে\" দেখায় খাদ্যটি পশুর নিজস্ব রেশন, নাকি শেড, ব্যাচ বা পেনের রেশন থেকে ভাগীদার পশুদের মধ্যে ভাগ করে দেওয়া অংশ।"),
        new("Cost (BDT) = Feed (kg) × Rate (BDT/kg); the rate is the cost per kg of the recipe on that day.",
            "খরচ (টাকা) = খাদ্য (কেজি) × দর (টাকা/কেজি); দর হলো সেই দিনের রেসিপির প্রতি কেজির খরচ।"),
        new("Monthly sub-totals and the period total add up feed and cost; rates are not added.",
            "মাসিক উপ-মোট ও পুরো সময়ের মোটে খাদ্য ও খরচ যোগ করা হয়; দর যোগ করা হয় না।"),
        new("If the animal was sold or died, only the days it was on the farm and fed are shown.",
            "পশুটি বিক্রি হলে বা মারা গেলে, শুধু খামারে থাকা ও খাওয়ানো দিনগুলো দেখানো হয়।"),
    ];

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

        var language = context.Language;
        var parts = new List<string>
        {
            new LocalizedText("Tag ", "ট্যাগ ").For(language) + FeedReportText.Tag(animal),
            FeedReportText.Species(animal.Species).For(language),
        };

        if (animal.LatestWeightKg is { } kg)
        {
            parts.Add(kg.ToString("0.#", CultureInfo.InvariantCulture) + new LocalizedText(" kg", " কেজি").For(language));
        }

        parts.Add(new LocalizedText("on farm since ", "খামারে আছে ").For(language) + FeedReportText.Day(animal.AcquisitionDate));
        parts.Add(FeedReportText.Period(context.Range("period")));

        return string.Join(" · ", parts);
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

        // A feeding voided after the animal left the farm keeps its allocation at 0 kg / 0 BDT;
        // such a row says nothing and reads like "fed nothing that day", so it is left out.
        var fed = allocations.Where(a => a.AllocatedKg != 0m || a.AllocatedCostBdt != 0m).ToList();

        if (fed.Count == 0)
        {
            return [];
        }

        // One lookup per *distinct* formula and rule set, not per row: a year of daily feed is
        // ~365 rows over a handful of recipes.
        var formulaNames = await ResolveFormulaNamesAsync(fed, cancellationToken).ConfigureAwait(false);
        var ruleSetNames = await ResolveRuleSetNamesAsync(fed, cancellationToken).ConfigureAwait(false);
        var notRecorded = FeedReportText.NotRecorded.For(context.Language);

        return fed
            .Select(a => new AnimalFeedingReportRow(
                a.EntryDate,
                ruleSetNames.GetValueOrDefault(a.FeedingPlanId, notRecorded),
                formulaNames.GetValueOrDefault(a.FormulaId, notRecorded),
                a.AllocatedKg,
                a.UnitCostBdtPerKg,
                a.AllocatedCostBdt,
                DescribeBasis(a).For(context.Language),
                a.HeadCountAtAllocation))
            // Same-day rations must sit together for the date cell to merge.
            .OrderBy(r => r.Date)
            .ThenBy(r => r.RuleSet, StringComparer.Ordinal)
            .ThenBy(r => r.Recipe, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Says plainly whether a figure was this animal's own ration or a share of a group's feed,
    /// and by what rule — an apportioned cost should never look like a measured one.
    /// </summary>
    private static LocalizedText DescribeBasis(AnimalFeedAllocation a)
    {
        if (a.Origin == FeedAllocationOrigin.IndividualPlan)
        {
            return new LocalizedText("Own ration", "নিজস্ব রেশন");
        }

        var method = a.Method switch
        {
            Domain.BusinessRules.FeedAllocationMethod.MetabolicWeight => new LocalizedText("by metabolic weight", "বিপাকীয় ওজন অনুযায়ী"),
            Domain.BusinessRules.FeedAllocationMethod.LiveWeight => new LocalizedText("by body weight", "দেহের ওজন অনুযায়ী"),
            _ => new LocalizedText("equal split", "সমান ভাগে"),
        };

        var later = a.IsBackfilled ? new LocalizedText(", filled in later", ", পরে যোগ করা") : new LocalizedText(string.Empty, string.Empty);

        return new LocalizedText(
            $"Share of group feed ({method.En}{later.En})",
            $"দলের খাদ্যের অংশ ({method.Bn}{later.Bn})");
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
            if (ruleSet is not null)
            {
                byPlan[plan.Id] = ruleSet.Name;
            }
        }

        return byPlan;
    }
}

/// <summary>
/// Plain-language wording shared by the feeding and inventory reports: enum values a farmer would
/// not recognise ("CattleBeef", "LowStock") turned into words, in both report languages, and dates
/// and periods written the way people say them.
/// </summary>
internal static class FeedReportText
{
    public static LocalizedText NotRecorded { get; } = new("Not recorded", "রেকর্ড নেই");

    public static LocalizedText Species(AnimalSpecies species) => species switch
    {
        AnimalSpecies.CattleBeef => new LocalizedText("Beef cattle", "মাংসের গরু"),
        AnimalSpecies.CattleDairy => new LocalizedText("Dairy cattle", "দুগ্ধ গাভী"),
        AnimalSpecies.Goat => new LocalizedText("Goat", "ছাগল"),
        AnimalSpecies.Sheep => new LocalizedText("Sheep", "ভেড়া"),
        _ => new LocalizedText("Livestock", "গবাদিপশু"),
    };

    /// <summary>For DTOs that carry the species as its enum name ("CattleBeef").</summary>
    public static LocalizedText Species(string? species)
    {
        if (string.IsNullOrWhiteSpace(species))
        {
            return NotRecorded;
        }

        return Enum.TryParse<AnimalSpecies>(species, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? Species(parsed)
            : new LocalizedText(species, species);
    }

    public static LocalizedText Sex(AnimalSex sex) => sex switch
    {
        AnimalSex.Male => new LocalizedText("Male", "পুরুষ"),
        AnimalSex.Female => new LocalizedText("Female", "স্ত্রী"),
        _ => NotRecorded,
    };

    public static LocalizedText Status(AnimalStatus status) => status switch
    {
        AnimalStatus.Active => new LocalizedText("On farm", "খামারে আছে"),
        AnimalStatus.Quarantined => new LocalizedText("Quarantined", "আলাদা রাখা (কোয়ারেন্টাইন)"),
        AnimalStatus.Sold => new LocalizedText("Sold", "বিক্রিত"),
        AnimalStatus.Slaughtered => new LocalizedText("Slaughtered", "জবাইকৃত"),
        AnimalStatus.Dead => new LocalizedText("Died", "মৃত"),
        AnimalStatus.Transferred => new LocalizedText("Transferred out", "স্থানান্তরিত"),
        _ => NotRecorded,
    };

    public static LocalizedText FeedingStatus(DailyFeedingEntryStatus status) => status switch
    {
        DailyFeedingEntryStatus.Pending => new LocalizedText("Not yet confirmed", "এখনো নিশ্চিত হয়নি"),
        DailyFeedingEntryStatus.Confirmed => new LocalizedText("Fed (confirmed)", "খাওয়ানো হয়েছে (নিশ্চিত)"),
        DailyFeedingEntryStatus.Adjusted => new LocalizedText("Fed (amount changed)", "খাওয়ানো হয়েছে (পরিমাণ পরিবর্তিত)"),
        DailyFeedingEntryStatus.Skipped => new LocalizedText("Skipped", "বাদ দেওয়া হয়েছে"),
        DailyFeedingEntryStatus.Exception => new LocalizedText("Problem reported", "সমস্যা জানানো হয়েছে"),
        _ => NotRecorded,
    };

    public static LocalizedText ItemCategory(InventoryCategory category) => category switch
    {
        InventoryCategory.Feed => new LocalizedText("Feed", "খাদ্য"),
        InventoryCategory.Medicine => new LocalizedText("Medicine", "ওষুধ"),
        InventoryCategory.Vaccine => new LocalizedText("Vaccine", "টিকা"),
        InventoryCategory.Chemical => new LocalizedText("Chemical", "রাসায়নিক"),
        InventoryCategory.Equipment => new LocalizedText("Equipment", "যন্ত্রপাতি"),
        InventoryCategory.Consumable => new LocalizedText("Consumable supplies", "ব্যবহার্য সামগ্রী"),
        _ => new LocalizedText("Other", "অন্যান্য"),
    };

    public static LocalizedText StockStatus(InventoryStatus status) => status switch
    {
        InventoryStatus.Sufficient => new LocalizedText("OK", "পর্যাপ্ত"),
        InventoryStatus.LowStock => new LocalizedText("Low — reorder", "কম — অর্ডার দিন"),
        InventoryStatus.OutOfStock => new LocalizedText("Out of stock", "মজুদ শেষ"),
        InventoryStatus.Excess => new LocalizedText("More than needed", "প্রয়োজনের বেশি"),
        _ => NotRecorded,
    };

    /// <summary>"B-04", or "B-04 (Sold 12-Nov-2026)" once the animal has left the farm.</summary>
    public static string Tag(AnimalDto animal)
    {
        ArgumentNullException.ThrowIfNull(animal);

        var what = animal.Status switch
        {
            AnimalStatus.Sold => "Sold",
            AnimalStatus.Dead => "Died",
            AnimalStatus.Slaughtered => "Slaughtered",
            AnimalStatus.Transferred => "Left",
            _ => null,
        };

        if (what is null)
        {
            return animal.TagId;
        }

        var exit = animal.ExitDate ?? animal.SaleDate ?? animal.DeathDate;
        return exit is { } d
            ? $"{animal.TagId} ({what} {d.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)})"
            : $"{animal.TagId} ({what})";
    }

    /// <summary>"18 Nov 2026".</summary>
    public static string Day(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"1 Nov – 18 Nov 2026", or "18 Nov 2026" for a single day.</summary>
    public static string Period(DateRange period)
    {
        if (period.From == period.To)
        {
            return Day(period.From);
        }

        var from = period.From.Year == period.To.Year
            ? period.From.ToString("d MMM", CultureInfo.InvariantCulture)
            : Day(period.From);

        return $"{from} – {Day(period.To)}";
    }

    /// <summary>A month group band: "Month: November 2026".</summary>
    public static LocalizedText Month(DateOnly date)
    {
        var month = date.ToString("MMMM yyyy", CultureInfo.InvariantCulture);
        return new LocalizedText($"Month: {month}", $"মাস: {month}");
    }
}
