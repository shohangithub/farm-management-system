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

public sealed record IncomeStatementReportRow(
    string Section,
    string Code,
    string Account,
    decimal AmountBdt,
    decimal? PriorAmountBdt);

/// <summary>
/// Report FN8 — Statement of Profit or Loss for a period (default: the financial year to date),
/// with the same period of the previous year. Income is positive and costs negative, so each
/// section sub-totals to its effect on profit and the grand total is the net profit.
/// </summary>
public sealed class IncomeStatementReportDefinition : ReportDefinition<IncomeStatementReportRow>
{
    public override string Key => "finance.income-statement";

    public override LocalizedText Title => new("Statement of Profit or Loss", "লাভ-ক্ষতির বিবরণী");

    public override LocalizedText Description => new(
        "Revenue, cost of sales, gross profit, operating expenses (incl. depreciation), finance costs and net profit, compared with the same period last year.",
        "আয়, বিক্রীত পণ্যের ব্যয়, মোট মুনাফা, পরিচালন ব্যয় (অবচয়সহ), আর্থিক ব্যয় ও নিট মুনাফা — গত বছরের একই সময়ের সাথে তুলনাসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("fromDate", new LocalizedText("From (blank = financial year start)", "শুরু (খালি = অর্থবছরের শুরু)"), required: false, @default: null),
        ReportParameter.Date("toDate", new LocalizedText("To", "পর্যন্ত"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<IncomeStatementReportRow>> Columns =>
    [
        ReportColumn<IncomeStatementReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<IncomeStatementReportRow>.Text("account", new LocalizedText("Particulars", "বিবরণ"), r => r.Account, width: 4f),
        ReportColumn<IncomeStatementReportRow>.Money("amount", new LocalizedText("Current (BDT)", "চলতি (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 30f, aggregate: ReportAggregate.Sum),
        ReportColumn<IncomeStatementReportRow>.Money("prior", new LocalizedText("Previous (BDT)", "পূর্ববর্তী (টাকা)"), r => r.PriorAmountBdt, decimals: 2, widthMm: 30f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<IncomeStatementReportRow>? Group =>
        ReportGroup<IncomeStatementReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var t = data.Totals;
        var result = t.NetProfitBdt >= 0 ? "profit" : "loss";
        return $"{data.FromDate:dd-MMM-yyyy} to {data.ToDate:dd-MMM-yyyy} — Gross profit {t.GrossProfitBdt:N2} ({t.GrossMarginPercent:0.#}%), net {result} {Math.Abs(t.NetProfitBdt):N2} BDT";
    }

    protected override async Task<IReadOnlyList<IncomeStatementReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        var rows = new List<IncomeStatementReportRow>();
        var number = 0;
        foreach (var section in data.Sections)
        {
            number++;
            var sign = section.Key == "revenue" ? 1m : -1m;
            rows.AddRange(section.Lines.Select(l => new IncomeStatementReportRow(
                $"{number}. {section.Name}",
                l.AccountCode,
                l.AccountName,
                sign * l.AmountBdt,
                l.PriorAmountBdt is { } p ? sign * p : null)));
        }

        return rows;
    }

    private static async Task<IncomeStatementDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        DateTime? from = context.Has("fromDate") ? context.Date("fromDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null;
        var to = context.Date("toDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return await context.Send(new GetIncomeStatementQuery(farmId, from, to), cancellationToken).ConfigureAwait(false);
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
