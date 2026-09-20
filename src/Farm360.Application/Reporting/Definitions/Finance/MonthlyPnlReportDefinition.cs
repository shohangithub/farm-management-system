using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record MonthlyPnlReportRow(
    string Section,
    string Category,
    decimal AmountBdt);

/// <summary>
/// Report FN3 — Monthly Profit and Loss Statement with categorized income and expenditure.
/// </summary>
public sealed class MonthlyPnlReportDefinition : ReportDefinition<MonthlyPnlReportRow>
{
    public override string Key => "finance.monthly-pnl";

    public override LocalizedText Title => new("Monthly Profit & Loss Statement", "মাসিক লাভ ও ক্ষতি বিবরণী");

    public override LocalizedText Description => new(
        "Detailed breakdown of income and operating expenses by category for a specific month.",
        "নির্দিষ্ট মাসের জন্য খাতভিত্তিক আয় ও পরিচালনা ব্যয়ের বিস্তারিত লাভ-ক্ষতি বিবরণী।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Number("year", new LocalizedText("Year", "বছর"), @default: 2026, required: true),
        ReportParameter.Select(
            "month",
            new LocalizedText("Month", "মাস"),
            [
                new ReportSelectOption("1", new LocalizedText("January", "জানুয়ারি")),
                new ReportSelectOption("2", new LocalizedText("February", "ফেব্রুয়ারি")),
                new ReportSelectOption("3", new LocalizedText("March", "মার্চ")),
                new ReportSelectOption("4", new LocalizedText("April", "এপ্রিল")),
                new ReportSelectOption("5", new LocalizedText("May", "মে")),
                new ReportSelectOption("6", new LocalizedText("June", "জুন")),
                new ReportSelectOption("7", new LocalizedText("July", "জুলাই")),
                new ReportSelectOption("8", new LocalizedText("August", "আগস্ট")),
                new ReportSelectOption("9", new LocalizedText("September", "সেপ্টেম্বর")),
                new ReportSelectOption("10", new LocalizedText("October", "অক্টোবর")),
                new ReportSelectOption("11", new LocalizedText("November", "নভেম্বর")),
                new ReportSelectOption("12", new LocalizedText("December", "ডিসেম্বর")),
            ],
            @default: "9",
            required: true),
    ];

    public override IReadOnlyList<ReportColumn<MonthlyPnlReportRow>> Columns =>
    [
        ReportColumn<MonthlyPnlReportRow>.Text("section", new LocalizedText("Classification", "শ্রেণীবিভাগ"), r => r.Section, width: 28f, relative: false),
        ReportColumn<MonthlyPnlReportRow>.Text("category", new LocalizedText("Account Category", "হিসাবের খাত"), r => r.Category, width: 3.5f),
        ReportColumn<MonthlyPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<MonthlyPnlReportRow>? Group =>
        ReportGroup<MonthlyPnlReportRow>.By(
            r => r.Section,
            r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var year = context.WholeNumber("year", context.Today.Year);
        var month = context.WholeNumber("month", context.Today.Month);
        var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(Math.Clamp(month, 1, 12));
        return $"Operating Performance for {monthName} {year}";
    }

    protected override async Task<IReadOnlyList<MonthlyPnlReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var year = context.WholeNumber("year", context.Today.Year);
        var month = context.WholeNumber("month", context.Today.Month);

        var data = await context
            .Send(new GetMonthlyPnLReportQuery(farmId, year, month), cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            return [];
        }

        var rows = new List<MonthlyPnlReportRow>();

        // Income Categories
        foreach (var (cat, amount) in data.IncomeByCategory.OrderBy(k => k.Key))
        {
            rows.Add(new MonthlyPnlReportRow("1. Operating Income", SplitCamelCase(cat), amount));
        }

        // Expense Categories
        foreach (var (cat, amount) in data.ExpenseByCategory.OrderBy(k => k.Key))
        {
            rows.Add(new MonthlyPnlReportRow("2. Operating Expenses", SplitCamelCase(cat), amount));
        }

        return rows;
    }

    private static string SplitCamelCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " ");
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
