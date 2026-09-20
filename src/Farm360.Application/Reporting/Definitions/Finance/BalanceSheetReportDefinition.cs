using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record BalanceSheetReportRow(
    string Section,
    string Code,
    string LineItem,
    string Notes,
    decimal AmountBdt);

/// <summary>
/// Report FN2 — Statement of Financial Position showing assets, liabilities, and owner/investor equity.
/// </summary>
public sealed class BalanceSheetReportDefinition : ReportDefinition<BalanceSheetReportRow>
{
    public override string Key => "finance.balance-sheet";

    public override LocalizedText Title => new("Balance Sheet Report", "উদ্বৃত্তপত্র প্রতিবেদন");

    public override LocalizedText Description => new(
        "Statement of financial position showing assets, liabilities, and owner/investor equity.",
        "সম্পদ, দায় এবং মালিক/বিনিয়োগকারীদের ইকুইটি প্রদর্শনকারী আর্থিক অবস্থার বিবরণী।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("asOfDate", new LocalizedText("As of Date", "তারিখ অনুযায়ী"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<BalanceSheetReportRow>> Columns =>
    [
        ReportColumn<BalanceSheetReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 20f, relative: false),
        ReportColumn<BalanceSheetReportRow>.Text("item", new LocalizedText("Line Item", "খাত"), r => r.LineItem, width: 3.2f),
        ReportColumn<BalanceSheetReportRow>.Text("notes", new LocalizedText("Notes / Basis", "নোট / ভিত্তি"), r => r.Notes, width: 2.2f),
        ReportColumn<BalanceSheetReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 30f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<BalanceSheetReportRow>? Group =>
        ReportGroup<BalanceSheetReportRow>.By(
            r => r.Section,
            r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var asOf = context.Date("asOfDate");
        return $"Statement of Financial Position as of {asOf:dd-MMM-yyyy}";
    }

    protected override async Task<IReadOnlyList<BalanceSheetReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var asOfDate = context.Date("asOfDate").ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var data = await context
            .Send(new GetBalanceSheetQuery(farmId, asOfDate), cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            return [];
        }

        var rows = new List<BalanceSheetReportRow>();

        // Assets
        if (data.Assets?.Lines != null)
        {
            rows.AddRange(data.Assets.Lines.Select(l => new BalanceSheetReportRow(
                Section: "1. Assets",
                Code: l.LineCode,
                LineItem: l.LineName,
                Notes: l.Notes ?? "—",
                AmountBdt: l.AmountBdt)));
        }

        // Liabilities
        if (data.Liabilities?.Lines != null)
        {
            rows.AddRange(data.Liabilities.Lines.Select(l => new BalanceSheetReportRow(
                Section: "2. Liabilities",
                Code: l.LineCode,
                LineItem: l.LineName,
                Notes: l.Notes ?? "—",
                AmountBdt: l.AmountBdt)));
        }

        // Equity
        if (data.Equity?.Lines != null)
        {
            rows.AddRange(data.Equity.Lines.Select(l => new BalanceSheetReportRow(
                Section: "3. Equity",
                Code: l.LineCode,
                LineItem: l.LineName,
                Notes: l.Notes ?? "—",
                AmountBdt: l.AmountBdt)));
        }

        return rows;
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
