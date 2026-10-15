using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record BalanceSheetReportRow(
    string Section,
    string Code,
    string LineItem,
    string Notes,
    decimal AmountBdt,
    decimal? PriorAmountBdt);

/// <summary>
/// Report FN2 — Statement of Financial Position in the auditor's layout: non-current and current
/// assets, liabilities, capital and reserves (retained earnings brought forward and the profit for
/// the year), with the previous financial year end alongside. A non-zero Difference is printed in
/// the subject line rather than plugged.
/// </summary>
public sealed class BalanceSheetReportDefinition : ReportDefinition<BalanceSheetReportRow>
{
    public override string Key => "finance.balance-sheet";

    public override LocalizedText Title => new("Statement of Financial Position", "আর্থিক অবস্থার বিবরণী (উদ্বৃত্তপত্র)");

    public override LocalizedText Description => new(
        "Balance sheet with non-current and current assets, liabilities and equity, compared with the previous financial year end.",
        "স্থায়ী ও চলতি সম্পদ, দায় এবং মালিকানা স্বত্ব — পূর্ববর্তী অর্থবছর শেষের সাথে তুলনাসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    // Assets and liabilities + equity are two sides of one equation; adding them up means nothing.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("asOfDate", new LocalizedText("As of Date", "তারিখ অনুযায়ী"), required: false, @default: "today"),
        ReportParameter.Bool("comparative", new LocalizedText("Compare with previous year end", "পূর্ববর্তী বছর শেষের সাথে তুলনা"), @default: true),
    ];

    public override IReadOnlyList<ReportColumn<BalanceSheetReportRow>> Columns =>
    [
        ReportColumn<BalanceSheetReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<BalanceSheetReportRow>.Text("item", new LocalizedText("Particulars", "বিবরণ"), r => r.LineItem, width: 3.2f),
        ReportColumn<BalanceSheetReportRow>.Text("notes", new LocalizedText("Notes / Basis", "নোট / ভিত্তি"), r => r.Notes, width: 2.2f),
        ReportColumn<BalanceSheetReportRow>.Money("amount", new LocalizedText("Current (BDT)", "চলতি (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<BalanceSheetReportRow>.Money("prior", new LocalizedText("Previous (BDT)", "পূর্ববর্তী (টাকা)"), r => r.PriorAmountBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<BalanceSheetReportRow>? Group =>
        ReportGroup<BalanceSheetReportRow>.By(
            r => r.Section,
            r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var subject = $"As at {data.AsOfDate:dd-MMM-yyyy}";
        if (data.PriorDate is { } prior)
            subject += $" (previous: {prior:dd-MMM-yyyy})";
        subject += data.IsBalanced
            ? $" — Total assets {data.TotalAssetsBdt:N2} = Liabilities + Equity"
            : $" — DIFFERENCE {data.DifferenceBdt:N2} BDT (assets {data.TotalAssetsBdt:N2} vs liabilities + equity {data.TotalLiabilitiesAndEquityBdt:N2})";
        return subject;
    }

    protected override async Task<IReadOnlyList<BalanceSheetReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        var rows = new List<BalanceSheetReportRow>();
        void Add(string section, BalanceSheetSectionDto dto)
        {
            rows.AddRange(dto.Lines.Select(l => new BalanceSheetReportRow(
                l.Group is null ? section : $"{section} — {l.Group}",
                l.LineCode,
                l.LineName,
                l.Notes ?? "—",
                l.AmountBdt,
                l.PriorAmountBdt)));
        }

        Add("1. Assets", data.Assets);
        Add("2. Liabilities", data.Liabilities);
        Add("3. Equity", data.Equity);
        return rows;
    }

    private static async Task<BalanceSheetDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var asOfDate = context.Date("asOfDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var compare = context.Bool("comparative", true) ? (DateTime?)null : DateTime.MinValue;
        return await context
            .Send(new GetBalanceSheetQuery(farmId, asOfDate, compare), cancellationToken)
            .ConfigureAwait(false);
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
