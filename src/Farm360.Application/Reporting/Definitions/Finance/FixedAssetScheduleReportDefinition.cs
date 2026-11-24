using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One fixed asset's line on the schedule, with its category and method already in words.</summary>
public sealed record FixedAssetScheduleReportRow(
    string CategoryKey,
    string CategoryLabel,
    string Code,
    string Name,
    string Method,
    decimal CostOpeningBdt,
    decimal AdditionsBdt,
    decimal DisposalsBdt,
    decimal CostClosingBdt,
    decimal DepreciationOpeningBdt,
    decimal DepreciationChargeBdt,
    decimal DepreciationOnDisposalBdt,
    decimal DepreciationClosingBdt,
    decimal WrittenDownValueBdt);

/// <summary>
/// Report FN9 — Fixed-asset schedule (property, plant &amp; equipment) for a period: cost and
/// depreciation brought forward, additions, charge, disposals, carried forward and written-down value.
/// </summary>
public sealed class FixedAssetScheduleReportDefinition : ReportDefinition<FixedAssetScheduleReportRow>
{
    public override string Key => "finance.fixed-asset-schedule";

    public override LocalizedText Title => new("Fixed Asset Schedule", "স্থায়ী সম্পদের তফসিল");

    public override LocalizedText Description => new(
        "What the farm's sheds, machinery and vehicles cost, how much they have worn down (depreciation) and what they are worth on the books, for a period.",
        "নির্দিষ্ট সময়ে খামারের শেড, যন্ত্রপাতি ও যানবাহনের ক্রয়মূল্য, কতটা ক্ষয় হয়েছে (অবচয়) এবং খাতায় এখন কত মূল্য।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("fromDate", new LocalizedText("From (blank = financial year start)", "শুরু (খালি = অর্থবছরের শুরু)"), required: false, @default: null),
        ReportParameter.Date("toDate", new LocalizedText("To", "পর্যন্ত"), required: false, @default: "today"),
    ];

    // Every money column is a cost or depreciation amount for one asset, so adding them per
    // category and in total is meaningful — the totals are the balance-sheet figures.
    public override IReadOnlyList<ReportColumn<FixedAssetScheduleReportRow>> Columns =>
    [
        ReportColumn<FixedAssetScheduleReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<FixedAssetScheduleReportRow>.Text("name", new LocalizedText("Asset", "সম্পদ"), r => r.Name, width: 3f),
        ReportColumn<FixedAssetScheduleReportRow>.Text("method", new LocalizedText("Depreciation method & rate", "অবচয় পদ্ধতি ও হার"), r => r.Method, width: 24f, relative: false),
        ReportColumn<FixedAssetScheduleReportRow>.Money("costOpening", new LocalizedText("Cost at start (BDT)", "শুরুতে ক্রয়মূল্য (টাকা)"), r => r.CostOpeningBdt, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("additions", new LocalizedText("+ Bought (BDT)", "+ ক্রয় (টাকা)"), r => r.AdditionsBdt, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("disposals", new LocalizedText("− Sold / scrapped (BDT)", "− বিক্রয় / বাতিল (টাকা)"), r => r.DisposalsBdt, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("costClosing", new LocalizedText("Cost at end (BDT)", "শেষে ক্রয়মূল্য (টাকা)"), r => r.CostClosingBdt, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("depOpening", new LocalizedText("Depreciation at start (BDT)", "শুরুতে অবচয় (টাকা)"), r => r.DepreciationOpeningBdt, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("charge", new LocalizedText("+ This period (BDT)", "+ এই সময়ে (টাকা)"), r => r.DepreciationChargeBdt, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("depDisposal", new LocalizedText("− On sold assets (BDT)", "− বিক্রিত সম্পদে (টাকা)"), r => r.DepreciationOnDisposalBdt, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("depClosing", new LocalizedText("Depreciation at end (BDT)", "শেষে অবচয় (টাকা)"), r => r.DepreciationClosingBdt, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<FixedAssetScheduleReportRow>.Money("wdv", new LocalizedText("Book value at end (BDT)", "শেষে খাতা মূল্য (টাকা)"), r => r.WrittenDownValueBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<FixedAssetScheduleReportRow>? Group =>
        ReportGroup<FixedAssetScheduleReportRow>.By(r => r.CategoryKey, r => r.CategoryLabel);

    public override LocalizedText? GrandTotalLabel => new("All fixed assets", "সকল স্থায়ী সম্পদ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new(
            "Cost: cost at start + bought − sold or scrapped = cost at end.",
            "ক্রয়মূল্য: শুরুতে ক্রয়মূল্য + ক্রয় − বিক্রয় বা বাতিল = শেষে ক্রয়মূল্য।"),
        new(
            "Depreciation is the wear-and-tear charged as a cost each year: depreciation at start + this period − on sold assets = depreciation at end.",
            "অবচয় হলো প্রতি বছর খরচ হিসেবে ধরা ক্ষয়: শুরুতে অবচয় + এই সময়ে − বিক্রিত সম্পদে = শেষে অবচয়।"),
        new(
            "Book value at end = cost at end − depreciation at end; the total matches the fixed assets on the balance sheet.",
            "শেষে খাতা মূল্য = শেষে ক্রয়মূল্য − শেষে অবচয়; মোট অঙ্কটি উদ্বৃত্তপত্রের স্থায়ী সম্পদের সমান।"),
        new(
            "Reducing balance takes the rate off each year's starting value; straight line takes an equal amount each year. Book value is not a market price.",
            "ক্রমহ্রাসমান পদ্ধতিতে প্রতি বছরের শুরুর মূল্য থেকে হারে কমে; সরলরৈখিক পদ্ধতিতে প্রতি বছর সমান অঙ্ক কমে। খাতা মূল্য বাজারদর নয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var period = $"{StatementWording.Day(context, data.FromDate)} – {StatementWording.Day(context, data.ToDate)}";
        var t = data.Totals;
        string M(decimal v) => StatementWording.Money(context, v);
        return context.Language == ReportLanguage.Bn
            ? $"{period} · এই সময়ের অবচয় {M(t.DepreciationChargeBdt)} · শেষে খাতা মূল্য {M(t.WrittenDownValueBdt)} টাকা"
            : $"{period} · Depreciation this period {M(t.DepreciationChargeBdt)} · book value at end {M(t.WrittenDownValueBdt)} BDT";
    }

    protected override async Task<IReadOnlyList<FixedAssetScheduleReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        return data.Lines
            .OrderBy(l => CategoryOrder(l.Category))
            .ThenBy(l => l.Category, StringComparer.Ordinal)
            .ThenBy(l => l.Code, StringComparer.Ordinal)
            .Select(l => new FixedAssetScheduleReportRow(
                l.Category,
                CategoryLabel(l.Category, bn),
                l.Code,
                l.Name,
                MethodLabel(l.Method, l.RatePercent, bn),
                l.CostOpeningBdt,
                l.AdditionsBdt,
                l.DisposalsBdt,
                l.CostClosingBdt,
                l.DepreciationOpeningBdt,
                l.DepreciationChargeBdt,
                l.DepreciationOnDisposalBdt,
                l.DepreciationClosingBdt,
                l.WrittenDownValueBdt))
            .ToList();
    }

    // Category and method arrive as the domain enum names ("BuildingAndShed", "ReducingBalance").
    private static int CategoryOrder(string category) => category switch
    {
        "Land" => 0,
        "BuildingAndShed" => 1,
        "MachineryAndEquipment" => 2,
        "Vehicle" => 3,
        "FurnitureAndOffice" => 4,
        _ => 5,
    };

    private static string CategoryLabel(string category, bool bn) => category switch
    {
        "Land" => bn ? "জমি" : "Land",
        "BuildingAndShed" => bn ? "ভবন ও শেড" : "Buildings & sheds",
        "MachineryAndEquipment" => bn ? "যন্ত্রপাতি ও সরঞ্জাম" : "Machinery & equipment",
        "Vehicle" => bn ? "যানবাহন" : "Vehicles",
        "FurnitureAndOffice" => bn ? "আসবাব ও অফিস সামগ্রী" : "Furniture & office",
        "Other" => bn ? "অন্যান্য" : "Other",
        _ => category,
    };

    private static string MethodLabel(string method, decimal ratePercent, bool bn)
    {
        var rate = ratePercent.ToString("0.##", CultureInfo.InvariantCulture);
        return method switch
        {
            "ReducingBalance" => bn ? $"ক্রমহ্রাসমান {rate}%/বছর" : $"Reducing balance {rate}%/yr",
            "StraightLine" when ratePercent > 0 => bn ? $"সরলরৈখিক {rate}%/বছর" : $"Straight line {rate}%/yr",
            "StraightLine" => bn ? "সরলরৈখিক" : "Straight line",
            _ => $"{method} {rate}%",
        };
    }

    // The subject line and the body both need the schedule; load it once per run.
    private static Task<FixedAssetScheduleDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("fixed-asset-schedule", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<FixedAssetScheduleDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        DateTime? from = context.Has("fromDate") ? context.Date("fromDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
        var to = context.Date("toDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await context.Send(new GetFixedAssetScheduleQuery(farmId, from, to), cancellationToken).ConfigureAwait(false);
    }
}
