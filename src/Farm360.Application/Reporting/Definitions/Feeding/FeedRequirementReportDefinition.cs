using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Feeding.Queries.Planning;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Feeding;

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
    public override string Key => "feeding.requirement";

    public override LocalizedText Title => new("Feed Purchase Plan", "খাদ্য ক্রয় পরিকল্পনা");

    public override LocalizedText Description => new(
        "Items and recipes needed for the next 7, 10, 15 or 30 days for an animal, shed, batch or the whole farm — what is in stock and what to buy.",
        "পরবর্তী ৭, ১০, ১৫ বা ৩০ দিনের জন্য একটি পশু, শেড, ব্যাচ বা পুরো খামারের প্রয়োজনীয় খাদ্য — মজুদ কত আছে এবং কত কিনতে হবে।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    // Sections mix items (to buy) and recipes (to mix); one grand total would add unlike things.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
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
        ReportColumn<FeedRequirementReportRow>.Text("item", new LocalizedText("Item / Recipe", "উপকরণ / রেসিপি"), r => r.Item, width: 3f),
        ReportColumn<FeedRequirementReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<FeedRequirementReportRow>.Number("perDay", new LocalizedText("Per day", "প্রতিদিন"), r => r.PerDay, decimals: 3, widthMm: 20f),
        ReportColumn<FeedRequirementReportRow>.Number("total", new LocalizedText("Needed", "প্রয়োজন"), r => r.Total, decimals: 2, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Number("stock", new LocalizedText("In stock", "মজুদ"), r => r.InStock, decimals: 2, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Number("buy", new LocalizedText("To buy", "কিনতে হবে"), r => r.ToBuy, decimals: 2, widthMm: 22f),
        ReportColumn<FeedRequirementReportRow>.Text("packs", new LocalizedText("Packs", "প্যাক"), r => r.Packs, width: 1.4f),
        ReportColumn<FeedRequirementReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, widthMm: 24f),
        ReportColumn<FeedRequirementReportRow>.Text("usedIn", new LocalizedText("Used in", "ব্যবহার"), r => r.UsedIn, width: 2.2f),
    ];

    public override ReportGroup<FeedRequirementReportRow>? Group =>
        ReportGroup<FeedRequirementReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{data.Days} days, {data.FromDate:dd-MMM-yyyy} to {data.ToDate:dd-MMM-yyyy} · {data.Animals} animal(s) · "
             + $"feed cost ৳{data.TotalCostOfNeedBdt:N0}, to buy ৳{data.TotalCostToBuyBdt:N0}"
             + (data.Notes.Count > 0 ? " · " + string.Join(" ", data.Notes) : string.Empty);
    }

    protected override async Task<IReadOnlyList<FeedRequirementReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        var rows = new List<FeedRequirementReportRow>();
        rows.AddRange(data.Items.Select(i => new FeedRequirementReportRow(
            "1. Items to have in stock (cost = what to buy)",
            i.LinkedToStock ? i.Name : i.Name + " (not linked to stock)",
            i.BaseUnit, i.PerDay, i.Total, i.LinkedToStock ? i.InStock : null, i.ToBuy,
            i.PacksToBuy is { } packs ? $"{packs} × {i.PackName}" : "—",
            i.CostToBuyBdt,
            string.Join(", ", i.UsedIn))));

        rows.AddRange(data.Recipes.Select(r => new FeedRequirementReportRow(
            "2. Recipes to mix (cost = value of feed)", r.Recipe, "kg", r.PerDayKg, r.TotalKg, null, null, "—", r.CostBdt, string.Empty)));

        rows.AddRange(data.PerAnimal.Select(a => new FeedRequirementReportRow(
            "3. By animal (cost = value of feed)", a.Tag + (a.Shed is null ? string.Empty : $" · {a.Shed}"), "kg", a.PerDayKg, a.TotalKg,
            null, null, "—", a.CostBdt, a.WeightKg is { } w ? $"{w:0.#} kg now" : "no weight")));

        return rows;
    }

    private static async Task<FeedRequirementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
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
