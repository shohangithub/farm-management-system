using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>
/// Report FN9 — Fixed-asset schedule (property, plant &amp; equipment) for a period: cost and
/// depreciation brought forward, additions, charge, disposals, carried forward and written-down value.
/// </summary>
public sealed class FixedAssetScheduleReportDefinition : ReportDefinition<FixedAssetScheduleLineDto>
{
    public override string Key => "finance.fixed-asset-schedule";

    public override LocalizedText Title => new("Fixed Asset Schedule", "স্থায়ী সম্পদের তফসিল");

    public override LocalizedText Description => new(
        "Cost, additions, disposals, depreciation and written-down value of every fixed asset for the financial year.",
        "অর্থবছরে প্রতিটি স্থায়ী সম্পদের ক্রয়মূল্য, সংযোজন, বিক্রয়, অবচয় ও লিখিত মূল্য।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("fromDate", new LocalizedText("From (blank = financial year start)", "শুরু (খালি = অর্থবছরের শুরু)"), required: false, @default: null),
        ReportParameter.Date("toDate", new LocalizedText("To", "পর্যন্ত"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<FixedAssetScheduleLineDto>> Columns =>
    [
        ReportColumn<FixedAssetScheduleLineDto>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<FixedAssetScheduleLineDto>.Text("name", new LocalizedText("Asset", "সম্পদ"), r => r.Name, width: 3f),
        ReportColumn<FixedAssetScheduleLineDto>.Number("rate", new LocalizedText("Rate %", "হার %"), r => r.RatePercent, decimals: 2, widthMm: 14f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("costOpening", new LocalizedText("Cost b/f", "ক্রয়মূল্য প্রারম্ভিক"), r => r.CostOpeningBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("additions", new LocalizedText("Additions", "সংযোজন"), r => r.AdditionsBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("disposals", new LocalizedText("Disposals", "বিক্রয়/বাতিল"), r => r.DisposalsBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("costClosing", new LocalizedText("Cost c/f", "ক্রয়মূল্য সমাপনী"), r => r.CostClosingBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("depOpening", new LocalizedText("Dep. b/f", "অবচয় প্রারম্ভিক"), r => r.DepreciationOpeningBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("charge", new LocalizedText("Charge", "চলতি অবচয়"), r => r.DepreciationChargeBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("depDisposal", new LocalizedText("On disposal", "বিক্রয়ে সমন্বয়"), r => r.DepreciationOnDisposalBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("depClosing", new LocalizedText("Dep. c/f", "অবচয় সমাপনী"), r => r.DepreciationClosingBdt, widthMm: 22f),
        ReportColumn<FixedAssetScheduleLineDto>.Money("wdv", new LocalizedText("WDV", "লিখিত মূল্য"), r => r.WrittenDownValueBdt, widthMm: 24f),
    ];

    public override ReportGroup<FixedAssetScheduleLineDto>? Group =>
        ReportGroup<FixedAssetScheduleLineDto>.By(r => r.Category, r => r.Category);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return $"{data.FromDate:dd-MMM-yyyy} to {data.ToDate:dd-MMM-yyyy}";
    }

    protected override async Task<IReadOnlyList<FixedAssetScheduleLineDto>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return data.Lines
            .OrderBy(l => l.Category, StringComparer.Ordinal)
            .ThenBy(l => l.Code, StringComparer.Ordinal)
            .ToList();
    }

    private static async Task<FixedAssetScheduleDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        DateTime? from = context.Has("fromDate") ? context.Date("fromDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
        var to = context.Date("toDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await context.Send(new GetFixedAssetScheduleQuery(farmId, from, to), cancellationToken).ConfigureAwait(false);
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
