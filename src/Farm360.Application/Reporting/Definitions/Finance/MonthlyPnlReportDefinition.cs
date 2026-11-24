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

/// <summary>
/// One statement line. <paramref name="ShareOfSection"/> is a 0–1 ratio of total income (income
/// lines) or total expenses (expense lines).
/// </summary>
public sealed record MonthlyPnlReportRow(
    string Section,
    string Category,
    decimal AmountBdt,
    decimal? ShareOfSection = null);

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

    public override LocalizedText Title => new("Monthly Profit or Loss", "মাসিক লাভ বা ক্ষতি");

    public override LocalizedText Description => new(
        "Did the farm make a profit this month? Income by type, expenses by type, and the profit or loss left over.",
        "এই মাসে খামারে লাভ হয়েছে কি? ধরনভিত্তিক আয়, ধরনভিত্তিক খরচ, এবং শেষে লাভ বা ক্ষতি।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override LocalizedText? GrandTotalLabel => new("Net profit / (loss) for the month", "মাসের নিট লাভ / (ক্ষতি)");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Income is shown as plus and expenses as minus, so the bottom line is the month's profit; a minus sign means a loss.",
            "আয় যোগ (+) ও খরচ বিয়োগ (−) হিসেবে দেখানো হয়েছে, তাই শেষ লাইনটিই মাসের লাভ; বিয়োগ চিহ্ন মানে ক্ষতি।"),
        new("Share (%) is each line's part of total income (for income) or of total expenses (for expenses).",
            "অংশ (%) হলো আয়ের লাইনের জন্য মোট আয়ের অংশ, আর খরচের লাইনের জন্য মোট খরচের অংশ।"),
        new("Feed and medicine count when they are used, not when they are bought; stock still in the store is not an expense yet.",
            "খাদ্য ও ওষুধ কেনার সময় নয়, ব্যবহারের সময় খরচ ধরা হয়; গুদামে থাকা মজুদ এখনও খরচ নয়।"),
        new("Buying animals is not an expense; their cost appears as 'Cost of animals sold' in the month they are sold, or 'Animals that died' if they die.",
            "পশু কেনা খরচ নয়; পশু বিক্রি হলে সেই মাসে তার খরচ 'বিক্রিত পশুর ক্রয়মূল্য' হিসেবে, আর মারা গেলে 'মৃত পশুর ক্ষতি' হিসেবে আসে।"),
        new("Owner or investor money and loans are not income, and loan repayments are not expenses (only the interest is).",
            "মালিক বা বিনিয়োগকারীর টাকা ও ঋণ আয় নয়, এবং ঋণ পরিশোধ খরচ নয় (শুধু সুদ খরচ)।"),
    ];
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
        ReportColumn<MonthlyPnlReportRow>.Text("category", new LocalizedText("Income or expense", "আয় বা খরচের ধরন"), r => r.Category, width: 3.5f),
        ReportColumn<MonthlyPnlReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 36f, aggregate: ReportAggregate.Sum),
        ReportColumn<MonthlyPnlReportRow>.Percent("share", new LocalizedText("Share (%)", "অংশ (%)"), r => r.ShareOfSection, decimals: 1, widthMm: 22f),
    ];

    public override ReportGroup<MonthlyPnlReportRow>? Group =>
        ReportGroup<MonthlyPnlReportRow>.By(r => r.Section, r => r.Section);

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (year, month) = ReadPeriod(context);
        var name = MonthNames[month - 1];
        var yearText = year.ToString(CultureInfo.InvariantCulture);
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"{name.Bn} {(context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(yearText) : yearText)} মাসের হিসাব"
            : $"Month of {name.En} {yearText}");
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
    /// result. Largest lines first within each section. Public for tests.
    /// </summary>
    public static IReadOnlyList<MonthlyPnlReportRow> BuildRows(
        IReadOnlyDictionary<string, decimal> income,
        IReadOnlyDictionary<string, decimal> expenses,
        ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(income);
        ArgumentNullException.ThrowIfNull(expenses);

        var bn = language == ReportLanguage.Bn;
        var incomeSection = bn ? "১. আয় (খামারে যা এসেছে)" : "1. Income (money earned)";
        var expenseSection = bn ? "২. খরচ (খামারে যা খরচ হয়েছে)" : "2. Expenses (costs of running the farm)";

        var totalIncome = income.Values.Sum();
        var totalExpense = expenses.Values.Sum();
        decimal? Share(decimal value, decimal total) =>
            total != 0m ? decimal.Round(value / total, 4, MidpointRounding.AwayFromZero) : null;

        var rows = new List<MonthlyPnlReportRow>(income.Count + expenses.Count);
        rows.AddRange(income
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new MonthlyPnlReportRow(incomeSection, CategoryName(kv.Key, language), kv.Value, Share(kv.Value, totalIncome))));
        rows.AddRange(expenses
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new MonthlyPnlReportRow(expenseSection, CategoryName(kv.Key, language), -kv.Value, Share(kv.Value, totalExpense))));
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

    /// <summary>
    /// The ledger groups lines by category code ("LaborCost", "CostOfAnimalsSold"); a farmer reads
    /// "Labour", "Cost of animals sold". Unknown keys fall back to the words split apart.
    /// </summary>
    private static string CategoryName(string key, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        (string En, string Bn)? name = key switch
        {
            "AnimalSale" => ("Animal sales", "পশু বিক্রয়"),
            "MilkSale" => ("Milk sales", "দুধ বিক্রয়"),
            "ByproductSale" => ("By-product & manure sales", "উপজাত ও গোবর বিক্রয়"),
            "OtherIncome" => ("Other farm income", "অন্যান্য আয়"),
            "CostOfAnimalsSold" => ("Cost of animals sold (their purchase price)", "বিক্রিত পশুর ক্রয়মূল্য"),
            "LivestockLoss" => ("Animals that died (their cost)", "মৃত পশুর ক্ষতি"),
            "FeedCost" => ("Feed", "খাদ্য"),
            "VeterinaryCost" => ("Vet & treatment", "পশু চিকিৎসা"),
            "MedicineCost" => ("Medicines & vaccines", "ঔষধ ও টিকা"),
            "LaborCost" => ("Labour", "শ্রমিক মজুরি"),
            "Utilities" => ("Electricity, water & fuel", "বিদ্যুৎ, পানি ও জ্বালানি"),
            "Transport" => ("Transport", "পরিবহন"),
            "MiscellaneousExpense" => ("Other expenses", "বিবিধ খরচ"),
            "ConsumableExpense" => ("Daily supplies used", "দৈনিক ভোগ্যপণ্য"),
            "InventoryWriteOff" => ("Stock spoiled or lost", "নষ্ট বা হারানো মজুদ"),
            "InterestExpense" => ("Loan interest", "ঋণের সুদ"),
            "BadDebt" => ("Money owed that will not be paid", "অনাদায়ী পাওনা"),
            "AssetDisposal" => ("Loss / (gain) on selling equipment", "সরঞ্জাম বিক্রয়ে ক্ষতি / (লাভ)"),
            "Depreciation" => ("Wear and tear of sheds & equipment (depreciation)", "শেড ও সরঞ্জামের অবচয়"),
            _ => null,
        };

        if (name is { } n)
        {
            return bn ? n.Bn : n.En;
        }

        return SplitCamelCase(key);
    }

    private static string SplitCamelCase(string text) =>
        string.IsNullOrEmpty(text)
            ? text
            : System.Text.RegularExpressions.Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " ");
}
