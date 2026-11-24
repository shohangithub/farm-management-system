using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Feeding.Queries.Planning;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <param name="Section">Stable section key ("1-items", "2-recipes", "3-animals"); the printed band comes from it.</param>
public sealed record FeedRequirementReportRow(
    string Section,
    string Item,
    string Unit,
    decimal PerDay,
    decimal Total,
    decimal? InStock,
    decimal? ToBuy,
    string Packs,
    decimal CostBdt,
    string UsedIn);

/// <summary>
/// Report FD6 — Feed purchase plan: what the rations need for the next N days for one animal, a
/// shed, a batch or the whole farm — stock items to buy (in packs), recipes to mix, and the cost.
/// </summary>
public sealed class FeedRequirementReportDefinition : ReportDefinition<FeedRequirementReportRow>
{
    private const string ItemsSection = "1-items";
    private const string RecipesSection = "2-recipes";
    private const string AnimalsSection = "3-animals";

    public override string Key => "feeding.requirement";

    public override LocalizedText Title => new("Feed Purchase Plan", "খাদ্য ক্রয় পরিকল্পনা");

    public override LocalizedText Description => new(
        "What feed will I need for the next 7–60 days, how much is already in stock, and what must I buy?",
        "আগামী ৭–৬০ দিনে কত খাদ্য লাগবে, মজুদে কত আছে, আর কত কিনতে হবে?");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    // Sections mix items (to buy) and recipes (to mix); one grand total would add unlike things.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Select("days", new LocalizedText("Days", "দিন"),
        [
            new ReportSelectOption("7", new LocalizedText("7 days", "৭ দিন")),
            new ReportSelectOption("10", new LocalizedText("10 days", "১০ দিন")),
            new ReportSelectOption("15", new LocalizedText("15 days", "১৫ দিন")),
            new ReportSelectOption("30", new LocalizedText("30 days", "৩০ দিন")),
            new ReportSelectOption("60", new LocalizedText("60 days", "৬০ দিন")),
        ], @default: "15", required: true),
        ReportParameter.Date("fromDate", new LocalizedText("Starting", "শুরু"), required: false, @default: "today"),
        ReportParameter.Shed(),
        ReportParameter.Batch(),
        ReportParameter.Animal(required: false),
        ReportParameter.Bool("projectGrowth", new LocalizedText("Grow weights by daily gain", "দৈনিক বৃদ্ধি ধরে হিসাব"), @default: true),
    ];

    public override IReadOnlyList<ReportColumn<FeedRequirementReportRow>> Columns =>
    [
        ReportColumn<FeedRequirementReportRow>.Text("item", new LocalizedText("Item / recipe / animal", "উপকরণ / রেসিপি / পশু"), r => r.Item, width: 3f),
        ReportColumn<FeedRequirementReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<FeedRequirementReportRow>.Number("perDay", new LocalizedText("Needed per day", "প্রতিদিন লাগবে"), r => r.PerDay, decimals: 3, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Number("total", new LocalizedText("Needed for the period", "পুরো সময়ে লাগবে"), r => r.Total, decimals: 2, widthMm: 24f),
        ReportColumn<FeedRequirementReportRow>.Number("stock", new LocalizedText("In stock now", "এখন মজুদ"), r => r.InStock, decimals: 2, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Number("buy", new LocalizedText("To buy", "কিনতে হবে"), r => r.ToBuy, decimals: 2, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Text("packs", new LocalizedText("Packs to buy", "কত প্যাক কিনবেন"), r => r.Packs, width: 1.4f),
        ReportColumn<FeedRequirementReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<FeedRequirementReportRow>.Text("usedIn", new LocalizedText("Used in / note", "ব্যবহার / মন্তব্য"), r => r.UsedIn, width: 2.2f),
    ];

    public override ReportGroup<FeedRequirementReportRow>? Group =>
        ReportGroup<FeedRequirementReportRow>.ByLocalized(r => r.Section, r => SectionLabel(r.Section));

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Section 1 lists every stock item the rations use: \"To buy\" = needed for the period − in stock now (never below zero), rounded up to whole packs.",
            "অংশ ১-এ রেশনে লাগে এমন সব মজুদ উপকরণ: \"কিনতে হবে\" = পুরো সময়ে লাগবে − এখন মজুদ (শূন্যের নিচে নয়), পুরো প্যাকে উপরের দিকে রাউন্ড করা।"),
        new("Section 1 cost is only what you still have to buy; sections 2 and 3 show the value of all the feed, so the sections are not added together.",
            "অংশ ১-এর খরচ শুধু যা কিনতে বাকি; অংশ ২ ও ৩-এ সব খাদ্যের মূল্য দেখানো, তাই অংশগুলো একসাথে যোগ করা হয় না।"),
        new("Section 2 is how much of each recipe to mix; section 3 is how much each animal will eat.",
            "অংশ ২-এ কোন রেসিপি কত মেশাতে হবে; অংশ ৩-এ প্রতিটি পশু কত খাবে।"),
        new("Quantities are in the unit shown in the Unit column; with \"Grow weights by daily gain\" on, weight-based rations rise as the animals grow.",
            "পরিমাণগুলো একক কলামে দেখানো এককে; \"দৈনিক বৃদ্ধি ধরে হিসাব\" চালু থাকলে পশুর ওজন বাড়ার সঙ্গে ওজনভিত্তিক রেশনও বাড়ে।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        var period = FeedReportText.Period(new DateRange(data.FromDate, data.ToDate));
        var need = data.TotalCostOfNeedBdt.ToString("N0", CultureInfo.InvariantCulture);
        var buy = data.TotalCostToBuyBdt.ToString("N0", CultureInfo.InvariantCulture);

        var line = bn
            ? $"{data.Days} দিন: {period} · {data.Animals}টি পশু · মোট খাদ্যের মূল্য ৳{need} · কিনতে লাগবে ৳{buy}"
            : $"{data.Days} days: {period} · {data.Animals} animals · feed worth ৳{need} needed · ৳{buy} to buy";

        // Warnings from the plan (missing stock links, animals without a ration) are English only.
        return data.Notes.Count > 0 ? line + " · " + string.Join(" ", data.Notes) : line;
    }

    protected override async Task<IReadOnlyList<FeedRequirementReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var language = context.Language;
        var notInStockList = new LocalizedText(" (not in stock list)", " (মজুদ তালিকায় নেই)").For(language);

        var rows = new List<FeedRequirementReportRow>();
        rows.AddRange(data.Items.Select(i => new FeedRequirementReportRow(
            ItemsSection,
            i.LinkedToStock ? i.Name : i.Name + notInStockList,
            i.BaseUnit, i.PerDay, i.Total, i.LinkedToStock ? i.InStock : null, i.ToBuy,
            i.PacksToBuy is { } packs ? $"{packs} × {i.PackName}" : "—",
            i.CostToBuyBdt,
            string.Join(", ", i.UsedIn))));

        // A stock item given directly (not through a recipe) is needed in its own unit, not kg.
        rows.AddRange(data.Recipes.Select(r => new FeedRequirementReportRow(
            RecipesSection, r.Recipe, string.IsNullOrWhiteSpace(r.Unit) ? "kg" : r.Unit, r.PerDayKg, r.TotalKg, null, null, "—", r.CostBdt, string.Empty)));

        rows.AddRange(data.PerAnimal.Select(a => new FeedRequirementReportRow(
            AnimalsSection,
            a.Tag + (a.Shed is null ? string.Empty : $" · {a.Shed}"),
            "kg", a.PerDayKg, a.TotalKg, null, null, "—", a.CostBdt,
            a.WeightKg is { } w
                ? new LocalizedText("Weighs ", "ওজন ").For(language) + w.ToString("0.#", CultureInfo.InvariantCulture) + new LocalizedText(" kg now", " কেজি (এখন)").For(language)
                : new LocalizedText("Weight not recorded", "ওজন রেকর্ড নেই").For(language))));

        return rows;
    }

    private static LocalizedText SectionLabel(string section) => section switch
    {
        ItemsSection => new LocalizedText(
            "1. Stock items needed — cost is what you still have to buy",
            "১. প্রয়োজনীয় মজুদ উপকরণ — খরচ = যা এখনো কিনতে হবে"),
        RecipesSection => new LocalizedText(
            "2. Recipes to mix — cost is the value of the feed",
            "২. যে রেসিপি মেশাতে হবে — খরচ = খাদ্যের মূল্য"),
        _ => new LocalizedText(
            "3. Feed per animal — cost is the value of the feed",
            "৩. পশুপ্রতি খাদ্য — খরচ = খাদ্যের মূল্য"),
    };

    // The subject line and the body both need the plan; compute it once per run.
    private static Task<FeedRequirementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("feed-requirement", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<FeedRequirementDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        var days = context.WholeNumber("days", 15);
        var animalId = context.IdOrNull("animalId");
        return await context.Send(new GetFeedRequirementQuery(
            farmId,
            days,
            context.Date("fromDate"),
            animalId is { } a ? [a] : null,
            context.IdOrNull("shedId"),
            null,
            context.IdOrNull("batchId"),
            context.Bool("projectGrowth", true)), cancellationToken).ConfigureAwait(false);
    }
}
