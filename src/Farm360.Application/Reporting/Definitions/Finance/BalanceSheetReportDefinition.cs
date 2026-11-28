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
/// Shared wording helpers for the financial statements: dates as "19 Nov 2026" and amounts in the
/// house money format, with Bengali digits when the farm uses them.
/// </summary>
internal static class StatementWording
{
    public static string Pick(ReportContext context, string en, string bn) =>
        context.Language == ReportLanguage.Bn ? bn : en;

    public static string Day(ReportContext context, DateOnly day)
    {
        if (context.Language == ReportLanguage.Bn)
            return AnimalLabels.Day(day, ReportLanguage.Bn, context.BengaliNumerals);
        var text = day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(text) : text;
    }

    public static string Day(ReportContext context, DateTime day) => Day(context, DateOnly.FromDateTime(day));

    public static string Money(ReportContext context, decimal amount) =>
        ReportValueFormatter.Format(amount, ReportColumnType.Money, 2, context.BengaliNumerals);

    public static string Number(ReportContext context, string formatted) =>
        context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(formatted) : formatted;
}

/// <summary>
/// Report FN2 — Balance sheet (statement of financial position) in the auditor's layout: what the
/// farm owns, what it owes and the owners' share, with the previous financial year end alongside.
/// A non-zero Difference is printed in the subject line rather than plugged.
/// </summary>
public sealed class BalanceSheetReportDefinition : ReportDefinition<BalanceSheetReportRow>
{
    public override string Key => "finance.balance-sheet";

    public override LocalizedText Title => new(
        "Balance Sheet (Statement of Financial Position)",
        "উদ্বৃত্তপত্র (আর্থিক অবস্থার বিবরণী)");

    public override LocalizedText Description => new(
        "What the farm owns, what it owes, and the owner's and investors' share on a date — compared with the previous financial year end.",
        "নির্দিষ্ট তারিখে খামারের সম্পদ, দায় এবং মালিক ও বিনিয়োগকারীদের অংশ — পূর্ববর্তী অর্থবছর শেষের সাথে তুলনাসহ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    // Assets and liabilities + equity are two sides of one equation; adding them up means nothing.
    // Each section's sub-total is the figure that matters (total assets, total liabilities, total equity).
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("asOfDate", new LocalizedText("As of Date", "তারিখ অনুযায়ী"), required: false, @default: "today"),
        ReportParameter.Bool("comparative", new LocalizedText("Compare with previous year end", "পূর্ববর্তী বছর শেষের সাথে তুলনা"), @default: true),
    ];

    public override IReadOnlyList<ReportColumn<BalanceSheetReportRow>> Columns =>
    [
        ReportColumn<BalanceSheetReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 14f, relative: false),
        ReportColumn<BalanceSheetReportRow>.Text("item", new LocalizedText("Item", "বিবরণ"), r => r.LineItem, width: 3.2f),
        ReportColumn<BalanceSheetReportRow>.Text("notes", new LocalizedText("How it is worked out", "যেভাবে হিসাব করা"), r => r.Notes, width: 2.6f),
        ReportColumn<BalanceSheetReportRow>.Money("amount", new LocalizedText("On this date (BDT)", "এই তারিখে (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
        ReportColumn<BalanceSheetReportRow>.Money("prior", new LocalizedText("Previous year end (BDT)", "গত অর্থবছর শেষে (টাকা)"), r => r.PriorAmountBdt, decimals: 2, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<BalanceSheetReportRow>? Group =>
        ReportGroup<BalanceSheetReportRow>.By(r => r.Section, r => r.Section);

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new(
            "The sub-total under each section is its total: everything the farm owns (assets), everything it owes (liabilities), and the owner's and investors' share (equity).",
            "প্রতিটি অংশের নিচের উপ-মোটই সেই অংশের মোট: খামারের সব সম্পদ, সব দায়, এবং মালিক ও বিনিয়োগকারীদের অংশ (মালিকানা স্বত্ব)।"),
        new(
            "The books balance when total assets = total liabilities + total equity; the line under the title says whether they do.",
            "মোট সম্পদ = মোট দায় + মোট মালিকানা স্বত্ব হলে হিসাব মিলেছে; শিরোনামের নিচের লাইনে তা দেখানো হয়েছে।"),
        new(
            "Equity is what would be left for the owners if every asset were turned into cash at book value and every debt paid.",
            "সব সম্পদ খাতার মূল্যে নগদে রূপান্তর করে সব দেনা শোধ করলে মালিকদের যা থাকত, সেটাই মালিকানা স্বত্ব।"),
        new(
            "Animals and stock are shown at what they cost, not at today's market price; sheds and machinery at cost less wear-and-tear (depreciation).",
            "পশু ও মজুদ মালামাল ক্রয়মূল্যে দেখানো হয়েছে, আজকের বাজারদরে নয়; শেড ও যন্ত্রপাতি ক্রয়মূল্য থেকে ক্ষয় (অবচয়) বাদে।"),
        new(
            "Animals on the farm are shown at their purchase cost plus the feed & care spent on them so far (farm rule \"Carry feed & care costs of animals on the farm\"); that feed & care becomes a cost when the animal is sold or dies.",
            "খামারে থাকা পশু দেখানো হয়েছে ক্রয়মূল্য ও এ পর্যন্ত তাদের খাদ্য ও যত্নের খরচ মিলিয়ে (খামারের নিয়ম \"খামারে থাকা পশুর খাদ্য ও যত্নের খরচ পশুর মূল্যে ধরুন\"); পশু বিক্রি বা মারা গেলে সেই খরচ তখনকার খরচ হয়।"),
        new(
            "Lines starting \"Less:\" carry a minus sign and are taken off their section (depreciation, owner's drawings, profit paid out).",
            "\"বাদ:\" দিয়ে শুরু লাইনগুলো ঋণাত্মক এবং তাদের অংশ থেকে বাদ যায় (অবচয়, মালিকের উত্তোলন, বণ্টিত মুনাফা)।"),
    ];

    /// <summary>Plain-language name and basis for each line, by line code; the DTO wording is the fallback.</summary>
    private static readonly Dictionary<string, (string En, string Bn, string NoteEn, string NoteBn)> _wording = new(StringComparer.Ordinal)
    {
        ["1500"] = ("Sheds, machinery & equipment (at cost)", "শেড, যন্ত্রপাতি ও সরঞ্জাম (ক্রয়মূল্যে)",
            "From the fixed-asset register", "স্থায়ী সম্পদ রেজিস্টার থেকে"),
        ["1590"] = ("Less: wear-and-tear to date (accumulated depreciation)", "বাদ: এ পর্যন্ত ক্ষয় (পুঞ্জীভূত অবচয়)",
            "Depreciation charged so far", "এ পর্যন্ত ধার্যকৃত অবচয়"),
        ["1010"] = ("Cash & bank", "নগদ ও ব্যাংক",
            "Money received less money paid (cash book)", "প্রাপ্ত অর্থ বাদ প্রদত্ত অর্থ (নগদ বই)"),
        ["1100"] = ("Customers owe us (receivable)", "ক্রেতাদের কাছে পাওনা (প্রাপ্য)",
            "Credit sales not yet collected", "বাকিতে বিক্রি, এখনো আদায় হয়নি"),
        ["1150"] = ("Advances, deposits & prepaid expenses", "অগ্রিম, জামানত ও অগ্রিম পরিশোধিত খরচ",
            "Paid in advance, to be used or refunded later", "আগাম পরিশোধ, পরে সমন্বয় বা ফেরতযোগ্য"),
        ["1200"] = ("Feed, medicine & supplies in stock (inventory)", "মজুদে থাকা খাদ্য, ঔষধ ও সামগ্রী (মজুদ)",
            "Stock bought or brought in, less used, returned or written off — at cost", "ক্রয় বা আনা মজুদ, বাদ ব্যবহৃত, ফেরত বা বাতিল — ক্রয়মূল্যে"),
        ["1300"] = ("Animals on the farm: purchase cost", "খামারে থাকা পশু: ক্রয়মূল্য",
            "Animals bought, less cost of animals sold, slaughtered or dead", "ক্রয়কৃত পশু, বাদ বিক্রিত, জবাইকৃত বা মৃত পশুর ক্রয়মূল্য"),
        ["1310"] = ("Animals on the farm: feed & care to date", "খামারে থাকা পশু: এ পর্যন্ত খাদ্য ও যত্নের খরচ",
            "Feed, health, labour and overhead spent on the animals still on the farm", "খামারে থাকা পশুর পেছনে খাদ্য, চিকিৎসা, শ্রম ও অন্যান্য খরচ"),
        ["2100"] = ("We owe suppliers (payable)", "সরবরাহকারীদের পাওনা (প্রদেয়)",
            "Bought on credit, not yet paid", "বাকিতে কেনা, এখনো পরিশোধ হয়নি"),
        ["2010"] = ("Loans still to repay (principal)", "পরিশোধযোগ্য ঋণ (আসল)",
            "Loans received less principal repaid; interest is an expense", "গৃহীত ঋণ বাদ পরিশোধিত আসল; সুদ খরচ হিসেবে ধরা হয়"),
        ["2200"] = ("Expenses due but not yet paid (provisions & accruals)", "বকেয়া খরচ, এখনো পরিশোধ হয়নি (সঞ্চিতি)",
            "Costs incurred but not yet paid or billed", "খরচ হয়েছে কিন্তু এখনো পরিশোধ বা বিল হয়নি"),
        ["2300"] = ("Other amounts owed", "অন্যান্য দায়", "—", "—"),
        ["2150"] = ("Customers paid us in advance", "ক্রেতাদের অগ্রিম প্রদান",
            "Collected beyond what their invoices come to", "বিলের চেয়ে বেশি আদায় হয়েছে"),
        ["2020"] = ("Bank overdraft / cash shortfall", "ব্যাংক ওভারড্রাফট / নগদ ঘাটতি",
            "More recorded as paid than received — check opening balances", "প্রাপ্তির চেয়ে বেশি প্রদান লেখা হয়েছে — প্রারম্ভিক জের দেখুন"),
        ["3010"] = ("Investors' money in the farm (investor capital)", "খামারে বিনিয়োগকারীদের অর্থ (বিনিয়োগ মূলধন)",
            "Invested less withdrawn", "বিনিয়োগ বাদ উত্তোলন"),
        ["3200"] = ("Owner's money in the farm (owner's capital)", "খামারে মালিকের অর্থ (মালিকের মূলধন)",
            "Cash, stock, animals and assets the owner brought in", "মালিকের আনা নগদ, মজুদ, পশু ও সম্পদ"),
        ["3210"] = ("Less: taken out by the owner (drawings)", "বাদ: মালিকের উত্তোলন",
            "Cash taken out for personal use", "ব্যক্তিগত প্রয়োজনে তোলা নগদ"),
        ["3900"] = ("Profit kept from earlier years (retained earnings)", "পূর্ববর্তী বছরগুলোর সঞ্চিত মুনাফা",
            "Profit and loss of earlier financial years", "আগের অর্থবছরগুলোর লাভ-ক্ষতি"),
        ["3950"] = ("Profit / (loss) this financial year", "চলতি অর্থবছরের মুনাফা / (ক্ষতি)",
            "Same as the bottom line of the Profit & Loss statement", "লাভ-ক্ষতির বিবরণীর শেষ লাইনের সমান"),
        ["3110"] = ("Less: profit paid out to investors", "বাদ: বিনিয়োগকারীদের দেওয়া মুনাফা",
            "Profit already distributed", "ইতিমধ্যে বণ্টিত মুনাফা"),
    };

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => StatementWording.Money(context, v);
        var bn = context.Language == ReportLanguage.Bn;

        var subject = bn
            ? $"{StatementWording.Day(context, data.AsOfDate)} তারিখে"
            : $"As at {StatementWording.Day(context, data.AsOfDate)}";
        if (data.PriorDate is { } prior)
        {
            subject += bn
                ? $" (তুলনা: {StatementWording.Day(context, prior)})"
                : $" (compared with {StatementWording.Day(context, prior)})";
        }

        var liabilities = data.Liabilities.TotalBdt;
        var equity = data.Equity.TotalBdt;
        subject += data.IsBalanced
            ? (bn
                ? $" · সম্পদ {M(data.TotalAssetsBdt)} = দায় {M(liabilities)} + মালিকানা স্বত্ব {M(equity)} টাকা — হিসাব মিলেছে"
                : $" · Assets {M(data.TotalAssetsBdt)} = Liabilities {M(liabilities)} + Equity {M(equity)} BDT — balanced")
            : (bn
                ? $" · গরমিল {M(data.DifferenceBdt)} টাকা: সম্পদ {M(data.TotalAssetsBdt)} বনাম দায় + মালিকানা স্বত্ব {M(data.TotalLiabilitiesAndEquityBdt)} — প্রারম্ভিক জের দেখুন"
                : $" · DIFFERENCE {M(data.DifferenceBdt)} BDT: assets {M(data.TotalAssetsBdt)} vs liabilities + equity {M(data.TotalLiabilitiesAndEquityBdt)} — check the opening balances");
        return subject;
    }

    protected override async Task<IReadOnlyList<BalanceSheetReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;

        var rows = new List<BalanceSheetReportRow>();
        void Add(string section, BalanceSheetSectionDto dto)
        {
            // The statement's own layout order: long-term assets first, then current; capital, then reserves.
            foreach (var l in dto.Lines)
            {
                string name;
                string note;
                if (_wording.TryGetValue(l.LineCode, out var w))
                {
                    name = bn ? w.Bn : w.En;
                    note = bn ? w.NoteBn : w.NoteEn;
                }
                else
                {
                    name = bn && !string.IsNullOrWhiteSpace(l.NameBn) ? l.NameBn : l.LineName;
                    note = l.Notes ?? "—";
                }

                rows.Add(new BalanceSheetReportRow(section, l.LineCode, name, note, l.AmountBdt, l.PriorAmountBdt));
            }
        }

        Add(bn ? "১. খামারের যা আছে (সম্পদ)" : "1. What the farm owns (assets)", data.Assets);
        Add(bn ? "২. খামারের যা দেনা (দায়)" : "2. What the farm owes (liabilities)", data.Liabilities);
        Add(bn ? "৩. মালিক ও বিনিয়োগকারীদের অংশ (মালিকানা স্বত্ব)" : "3. Owner's and investors' share (equity)", data.Equity);
        return rows;
    }

    // The subject line and the body both need the statement; load it once per run.
    private static Task<BalanceSheetDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("balance-sheet", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<BalanceSheetDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var farmId = context.FarmId();
        var asOfDate = context.Date("asOfDate").ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var compare = context.Bool("comparative", true) ? (DateTime?)null : DateTime.MinValue;
        return await context
            .Send(new GetBalanceSheetQuery(farmId, asOfDate, compare), cancellationToken)
            .ConfigureAwait(false);
    }
}
