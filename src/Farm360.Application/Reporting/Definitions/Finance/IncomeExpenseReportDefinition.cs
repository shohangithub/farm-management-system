using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record IncomeExpenseReportRow(
    string Section,
    DateTime? Date,
    string VoucherNo,
    string Head,
    string Description,
    decimal? ReceiptBdt,
    decimal? PaymentBdt);

/// <summary>
/// Report FN6 — Daily / date-range Income &amp; Expense statement in classic two-sided cash-book form:
/// opening balance + receipts on one side, payments + closing balance on the other, so the two
/// column totals always agree.
/// </summary>
public sealed class IncomeExpenseReportDefinition : ReportDefinition<IncomeExpenseReportRow>
{
    public override string Key => "finance.income-expense";

    public override LocalizedText Title => new("Income & Expense Statement", "আয়-ব্যয় বিবরণী");

    public override LocalizedText Description => new(
        "Daily or date-range cash book: opening balance, every income and expense, and closing balance.",
        "দৈনিক বা তারিখভিত্তিক ক্যাশ বই: ওপেনিং ব্যালেন্স, সকল আয় ও ব্যয় এবং ক্লোজিং ব্যালেন্স।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.DateRange("period", @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<IncomeExpenseReportRow>> Columns =>
    [
        ReportColumn<IncomeExpenseReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<IncomeExpenseReportRow>.Text("voucher", new LocalizedText("Voucher", "রশিদ নং"), r => r.VoucherNo, width: 26f, relative: false),
        ReportColumn<IncomeExpenseReportRow>.Text("head", new LocalizedText("Account Head", "খাত"), r => r.Head, width: 1.6f),
        ReportColumn<IncomeExpenseReportRow>.Text("description", new LocalizedText("Description", "বিবরণ"), r => r.Description, width: 2.6f),
        ReportColumn<IncomeExpenseReportRow>.Money("receipt", new LocalizedText("Income (BDT)", "আয় (টাকা)"), r => r.ReceiptBdt, widthMm: 26f),
        ReportColumn<IncomeExpenseReportRow>.Money("payment", new LocalizedText("Expense (BDT)", "ব্যয় (টাকা)"), r => r.PaymentBdt, widthMm: 26f),
    ];

    public override ReportGroup<IncomeExpenseReportRow>? Group =>
        ReportGroup<IncomeExpenseReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var range = context.Range("period");
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var period = range.From == range.To ? $"{range.From:dd-MMM-yyyy}" : range.ToString();
        return $"{period} · Opening {data.OpeningBalanceBdt:N2} · Income {data.TotalIncomeBdt:N2} · " +
               $"Expense {data.TotalExpenseBdt:N2} · Closing {data.ClosingBalanceBdt:N2} BDT";
    }

    protected override async Task<IReadOnlyList<IncomeExpenseReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);

        var rows = new List<IncomeExpenseReportRow>
        {
            new("1. Opening Balance", data.FromDate, "OPENING", "Opening Balance", "Cash brought forward",
                data.OpeningBalanceBdt, null),
        };

        foreach (var t in data.IncomeTransactions)
        {
            rows.Add(new IncomeExpenseReportRow("2. Income", t.TransactionDate, t.VoucherNo,
                $"{t.Code} {t.HeadName}", t.Description, t.AmountBdt, null));
        }

        foreach (var t in data.ExpenseTransactions)
        {
            rows.Add(new IncomeExpenseReportRow("3. Expense", t.TransactionDate, t.VoucherNo,
                $"{t.Code} {t.HeadName}", t.Description, null, t.AmountBdt));
        }

        rows.Add(new IncomeExpenseReportRow("4. Closing Balance", data.ToDate, "CLOSING", "Closing Balance",
            "Cash carried forward", null, data.ClosingBalanceBdt));

        return rows;
    }

    private static async Task<IncomeExpenseReportDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = await FinanceReportFarm.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var range = context.Range("period");
        return await context.Send(
            new GetIncomeExpenseReportQuery(
                farmId,
                range.From.ToDateTime(TimeOnly.MinValue),
                range.To.ToDateTime(TimeOnly.MinValue)),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Farm resolution shared by the cash-book style finance reports.</summary>
internal static class FinanceReportFarm
{
    public static async Task<Guid> ResolveAsync(ReportContext context, CancellationToken cancellationToken)
    {
        if (context.IdOrNull("farmId") is { } id && id != Guid.Empty)
            return id;
        if (context.AssignedFarmIds is { Count: > 0 } assigned && assigned[0] != Guid.Empty)
            return assigned[0];

        var farms = await context.Send(new GetAllFarmsQuery(), cancellationToken).ConfigureAwait(false);
        return farms.Count > 0 ? farms[0].Id : Guid.Empty;
    }
}
