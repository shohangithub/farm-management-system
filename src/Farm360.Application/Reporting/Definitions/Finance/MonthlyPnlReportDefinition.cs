using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record MonthlyPnlReportRow(
    string Section,
    string Category,
    decimal AmountBdt);

/// <summary>
/// Report FN3 — Monthly Profit and Loss Statement with categorized income and expenditure.
/// </summary>
/// <remarks>
/// Income is positive and expenses negative, so each section sub-totals to its effect on profit
/// and the bottom line is printed as what it is — "Net profit / (loss)" — rather than a "grand
/// total" that adds income to expenses.
/// </remarks>
public sealed class MonthlyPnlReportDefinition : ReportDefinition<MonthlyPnlReportRow>
{
    private static readonly (string En, string Bn)[] MonthNames =
    [
        ("January", "জানুয়ারি"), ("February", "ফেব্রুয়ারি"), ("March", "মার্চ"), ("April", "এপ্রিল"),
        ("May", "মে"), ("June", "জুন"), ("July", "জুলাই"), ("August", "আগস্ট"),
        ("September", "সেপ্টেম্বর"), ("October", "অক্টোবর"), ("November", "নভেম্বর"), ("December", "ডিসেম্বর"),
    ];

    public override string Key => "finance.monthly-pnl";

    public override LocalizedText Title => new("Monthly Profit & Loss Statement", "মাসিক লাভ ও ক্ষতি বিবরণী");

    public override LocalizedText Description => new(
        "Detailed breakdown of income and operating expenses by category for a specific month.",
        "নির্দিষ্ট মাসের জন্য খাতভিত্তিক আয় ও পরিচালনা ব্যয়ের বিস্তারিত লাভ-ক্ষতি বিবরণী।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override LocalizedText? GrandTotalLabel => new("Net profit / (loss) for the month", "মাসের নিট লাভ / (ক্ষতি)");

    /// <summary>
    /// Defaults are the current month in Bangladesh time, evaluated when the catalog is built —
    /// not a year and month frozen into the code. A blank value falls back to the current month in
    /// the farm's own zone at run time.
    /// </summary>
    public override IReadOnlyList<ReportParameter> Parameters
    {
        get
        {
            var today = ReportTimeZone.Today(DateTime.UtcNow, ReportTimeZone.Default);
            return
            [
                ReportParameter.Farm(required: true),
                ReportParameter.Number("year", new LocalizedText("Year", "বছর"), @default: today.Year, required: false),
                ReportParameter.Select(
                    "month",
                    new LocalizedText("Month", "মাস"),
                    MonthNames
                        .Select((m, i) => new ReportSelectOption(
                            (i + 1).ToString(CultureInfo.InvariantCulture),
                            new LocalizedText(m.En, m.Bn)))
                        .ToList(),
                    @default: today.Month.ToString(CultureInfo.InvariantCulture),
                    required: false),
            ];
        }
    }

    public override IReadOnlyList<ReportColumn<MonthlyPnlReportRow>> Columns =>
    [
        ReportColumn<MonthlyPnlReportRow>.Text("category", new LocalizedText("Account Category", "হিসাবের খাত"), r => r.Category, width: 3.5f),
        ReportColumn<MonthlyPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 36f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<MonthlyPnlReportRow>? Group =>
        ReportGroup<MonthlyPnlReportRow>.By(r => r.Section, r => r.Section);

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (year, month) = ReadPeriod(context);
        var name = MonthNames[month - 1];
        var yearText = ReportValueFormatter.Format(year, ReportColumnType.Text, 0);
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"{name.Bn} {(context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(yearText) : yearText)} মাসের পরিচালন ফলাফল"
            : $"Operating performance for {name.En} {yearText}");
    }

    protected override async Task<IReadOnlyList<MonthlyPnlReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (year, month) = ReadPeriod(context);

        var data = await context
            .Send(new GetMonthlyPnLReportQuery(context.FarmId(), year, month), cancellationToken)
            .ConfigureAwait(false);

        return data is null
            ? []
            : BuildRows(data.IncomeByCategory, data.ExpenseByCategory, context.Language);
    }

    /// <summary>
    /// The statement lines: income positive, expenses negative, so the column total is the net
    /// result. Public for tests.
    /// </summary>
    public static IReadOnlyList<MonthlyPnlReportRow> BuildRows(
        IReadOnlyDictionary<string, decimal> income,
        IReadOnlyDictionary<string, decimal> expenses,
        ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);

        var bn = language == ReportLanguage.Bn;
        var incomeSection = bn ? "১. পরিচালন আয়" : "1. Operating income";
        var expenseSection = bn ? "২. পরিচালন ব্যয়" : "2. Operating expenses";

        var rows = new List<MonthlyPnlReportRow>(income.Count + expenses.Count);
        rows.AddRange(income.OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(kv => new MonthlyPnlReportRow(incomeSection, SplitCamelCase(kv.Key), kv.Value)));
        rows.AddRange(expenses.OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(kv => new MonthlyPnlReportRow(expenseSection, SplitCamelCase(kv.Key), -kv.Value)));
        return rows;
    }

    private static (int Year, int Month) ReadPeriod(ReportContext context)
    {
        var year = context.WholeNumber("year", context.Today.Year);
        var month = context.WholeNumber("month", context.Today.Month);

        if (year is < 2000 or > 2100)
        {
            throw new ReportParameterException("year", "'Year' must be between 2000 and 2100.");
        }

        if (month is < 1 or > 12)
        {
            throw new ReportParameterException("month", "'Month' must be between 1 and 12.");
        }

        return (year, month);
    }

    private static string SplitCamelCase(string text) =>
        string.IsNullOrEmpty(text)
            ? text
            : System.Text.RegularExpressions.Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " ");
}
