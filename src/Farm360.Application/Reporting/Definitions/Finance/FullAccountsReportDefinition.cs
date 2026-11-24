using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Contracts.Finance;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record FullAccountsReportRow(
    string Section,
    string Code,
    string LineItem,
    string Notes,
    decimal? AmountBdt);

/// <summary>
/// Report FN7 — Full accounts statement for a period: cash position, operating P&amp;L, cost analysis,
/// stock and livestock, credit, investor capital, profit sharing, share capital and loans on one
/// printable statement.
/// </summary>
/// <remarks>
/// Sections mix balances, flows and sub-totals, so a column sum would add apples to oranges.
/// Totals are therefore explicit lines and the platform aggregates are switched off.
/// </remarks>
public sealed class FullAccountsReportDefinition : ReportDefinition<FullAccountsReportRow>
{
    public override string Key => "finance.full-accounts";

    public override LocalizedText Title => new("Full Accounts Statement", "সম্পূর্ণ হিসাব বিবরণী");

    public override LocalizedText Description => new(
        "The whole money picture for a period on one page: cash in and out, income, every cost incl. labour, stock and animals, credit, investors, profit share, shares and loans.",
        "এক বিবরণীতে নির্দিষ্ট সময়ের পুরো আর্থিক চিত্র: নগদ আসা-যাওয়া, আয়, শ্রমিকসহ সব খরচ, মজুদ ও পশু, বাকি লেনদেন, বিনিয়োগকারী, লভ্যাংশ, শেয়ার ও ঋণ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    // Every section mixes opening balances, movements and closing balances; nothing here may be added
    // down the page, so each total is its own labelled line.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<FullAccountsReportRow>> Columns =>
    [
        ReportColumn<FullAccountsReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 14f, relative: false),
        ReportColumn<FullAccountsReportRow>.Text("item", new LocalizedText("Item", "খাত"), r => r.LineItem, width: 2.6f),
        ReportColumn<FullAccountsReportRow>.Text("notes", new LocalizedText("Details", "বিস্তারিত"), r => r.Notes, width: 2.6f),
        ReportColumn<FullAccountsReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, widthMm: 30f, aggregate: ReportAggregate.None),
    ];

    public override ReportGroup<FullAccountsReportRow>? Group =>
        ReportGroup<FullAccountsReportRow>.By(r => r.Section, r => r.Section);

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new(
            "Each section ends with its own result line (closing cash, net operating profit, closing stock…); figures are never added down the page.",
            "প্রতিটি অংশ নিজস্ব ফলাফল লাইনে শেষ হয় (সমাপনী নগদ, নিট পরিচালন মুনাফা, সমাপনী মজুদ…); পৃষ্ঠার নিচে অঙ্ক যোগ করা হয় না।"),
        new(
            "Lines marked (+) add to the opening figure and (−) take away from it, so opening + (+) − (−) = closing.",
            "(+) চিহ্নিত লাইন প্রারম্ভিক অঙ্কে যোগ হয় এবং (−) চিহ্নিত লাইন বাদ যায়: প্রারম্ভিক + (+) − (−) = সমাপনী।"),
        new(
            "Net operating profit is farm income minus farm costs. Loans, investor and owner money move cash but are never profit.",
            "নিট পরিচালন মুনাফা = খামারের আয় − খামারের খরচ। ঋণ, বিনিয়োগকারী ও মালিকের অর্থ নগদ বাড়ায়-কমায়, কিন্তু কখনো মুনাফা নয়।"),
        new(
            "Stock and animals are assets at cost: buying them is not a cost until feed is used or an animal is sold or dies.",
            "মজুদ ও পশু ক্রয়মূল্যে সম্পদ: খাদ্য ব্যবহার না হওয়া বা পশু বিক্রি বা মারা না যাওয়া পর্যন্ত কেনাটা খরচ নয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var d = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => StatementWording.Money(context, v);
        var period = $"{StatementWording.Day(context, d.FromDate)} – {StatementWording.Day(context, d.ToDate)}";
        var profit = d.Operating.NetOperatingProfitBdt;

        return context.Language == ReportLanguage.Bn
            ? $"{period} · {(profit >= 0 ? "নিট পরিচালন মুনাফা" : "নিট পরিচালন ক্ষতি")} {M(Math.Abs(profit))} · শেষে নগদ {M(d.Cash.ClosingBalanceBdt)} টাকা"
            : $"{period} · {(profit >= 0 ? "Net operating profit" : "Net operating loss")} {M(Math.Abs(profit))} · cash at end {M(d.Cash.ClosingBalanceBdt)} BDT";
    }

    protected override async Task<IReadOnlyList<FullAccountsReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var d = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;
        string Day(DateTime date) => StatementWording.Day(context, date);
        static string N0(decimal v) => v.ToString("N0", CultureInfo.InvariantCulture);
        static string N2(decimal v) => v.ToString("N2", CultureInfo.InvariantCulture);
        static string P(decimal v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        var rows = new List<FullAccountsReportRow>();
        var sectionNumber = 0;
        string Section(string en, string bnText)
        {
            sectionNumber++;
            var n = sectionNumber.ToString(CultureInfo.InvariantCulture);
            return $"{(bn ? ReportValueFormatter.ToBengaliNumerals(n) : n)}. {T(en, bnText)}";
        }

        void Add(string section, string code, string item, string notes, decimal? amount) =>
            rows.Add(new FullAccountsReportRow(
                section,
                code,
                item,
                context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(notes) : notes,
                amount));

        // Cash
        var cash = Section("Cash in hand & bank", "হাতে ও ব্যাংকে নগদ");
        Add(cash, "1010", T("Cash at the start", "শুরুতে নগদ"), T($"On {Day(d.FromDate)}", $"{Day(d.FromDate)} তারিখে"), d.Cash.OpeningBalanceBdt);
        Add(cash, "", T("(+) Cash received", "(+) নগদ প্রাপ্তি"), T($"{d.Cash.TransactionCount} transaction(s) in the period", $"এই সময়ে {d.Cash.TransactionCount}টি লেনদেন"), d.Cash.TotalInflowBdt);
        Add(cash, "", T("(−) Cash paid out", "(−) নগদ প্রদান"), "", d.Cash.TotalOutflowBdt);
        Add(cash, "", T("Net cash in / (out)", "নিট নগদ প্রাপ্তি / (প্রদান)"), T("Received − paid", "প্রাপ্তি − প্রদান"), d.Cash.NetCashFlowBdt);
        Add(cash, "1010", T("Cash at the end", "শেষে নগদ"), T($"On {Day(d.ToDate)}", $"{Day(d.ToDate)} তারিখে"), d.Cash.ClosingBalanceBdt);

        // Operating income
        var revenue = Section("Farm income", "খামারের আয়");
        foreach (var l in d.Operating.RevenueLines)
            Add(revenue, l.Code, bn ? l.NameBn : l.Name, T($"{l.Count} entr(ies) · {l.PercentOfTotal:0.#}% of income", $"{l.Count}টি এন্ট্রি · আয়ের {l.PercentOfTotal:0.#}%"), l.TotalBdt);
        Add(revenue, "", T("Total farm income", "মোট খামার আয়"), "", d.Operating.TotalRevenueBdt);

        // Operating expenses
        var expense = Section("Farm costs", "খামারের খরচ");
        foreach (var l in d.Operating.ExpenseLines)
            Add(expense, l.Code, bn ? l.NameBn : l.Name, T($"{l.Count} entr(ies) · {l.PercentOfTotal:0.#}% of costs", $"{l.Count}টি এন্ট্রি · খরচের {l.PercentOfTotal:0.#}%"), l.TotalBdt);
        Add(expense, "", T("Total farm costs", "মোট খামার খরচ"), "", d.Operating.TotalExpenseBdt);
        Add(expense, "",
            d.Operating.NetOperatingProfitBdt >= 0 ? T("NET OPERATING PROFIT (income − costs)", "নিট পরিচালন মুনাফা (আয় − খরচ)") : T("NET OPERATING LOSS (income − costs)", "নিট পরিচালন ক্ষতি (আয় − খরচ)"),
            T($"Margin {P(d.Operating.ProfitMarginPercent)}% of income", $"আয়ের {P(d.Operating.ProfitMarginPercent)}% মার্জিন"),
            d.Operating.NetOperatingProfitBdt);

        // Cost analysis
        var costs = Section("Where the costs went", "খরচ কোথায় গেল");
        foreach (var g in d.Costs.Groups)
            Add(costs, "", bn ? g.NameBn : g.Name, T($"{g.Count} entr(ies) · {g.PercentOfTotal:0.#}% of costs", $"{g.Count}টি এন্ট্রি · খরচের {g.PercentOfTotal:0.#}%"), g.AmountBdt);
        Add(costs, "", T("Average cost per day", "দৈনিক গড় খরচ"), T($"Over {d.Days} day(s)", $"{d.Days} দিনের হিসাবে"), d.Costs.AverageDailyCostBdt);
        Add(costs, "", T("Recorded automatically", "স্বয়ংক্রিয়ভাবে লেখা"), T("From feeding, stock & livestock records", "খাদ্য, মজুদ ও পশুর রেকর্ড থেকে"), d.Costs.AutomatedPostingsBdt);
        Add(costs, "", T("Entered by hand", "হাতে লেখা"), T("Entered by staff", "কর্মীদের দ্বারা লেখা"), d.Costs.ManualPostingsBdt);

        // Stock & livestock (non-cash assets)
        var assets = Section("Stock & animals (assets at cost)", "মজুদ ও পশু (ক্রয়মূল্যে সম্পদ)");
        var a = d.Assets;
        Add(assets, "1200", T("Stock at the start", "শুরুতে মজুদ"), T("Feed, medicine & supplies", "খাদ্য, ঔষধ ও সামগ্রী"), a.InventoryOpeningBdt);
        Add(assets, "", T("(+) Stock bought", "(+) মজুদ ক্রয়"), T("Paid in cash or bought on credit", "নগদে বা বাকিতে কেনা"), a.StockPurchasedBdt);
        Add(assets, "", T("(+) Stock brought in", "(+) আনা মজুদ"), T("Opening / owner-supplied stock", "প্রারম্ভিক / মালিকের দেওয়া মজুদ"), a.StockBroughtInBdt);
        Add(assets, "", T("(−) Returned to suppliers", "(−) সরবরাহকারীকে ফেরত"), "", a.StockReturnedBdt);
        Add(assets, "", T("(−) Used (feed, medicine, consumables)", "(−) ব্যবহৃত (খাদ্য, ঔষধ, ভোগ্যপণ্য)"), T("Counted as a cost", "খরচ হিসেবে ধরা"), a.StockConsumedBdt);
        Add(assets, "", T("(−) Written off", "(−) বাতিল"), T("Damaged / expired / lost", "নষ্ট / মেয়াদোত্তীর্ণ / হারানো"), a.StockWrittenOffBdt);
        Add(assets, "1200", T("Stock at the end", "শেষে মজুদ"), "", a.InventoryClosingBdt);
        Add(assets, "1300", T("Animals at the start (at cost)", "শুরুতে পশু (ক্রয়মূল্যে)"), "", a.LivestockOpeningBdt);
        Add(assets, "", T("(+) Animals bought", "(+) পশু ক্রয়"), "", a.AnimalsPurchasedBdt);
        Add(assets, "", T("(+) Animals brought in", "(+) আনা পশু"), T("Owner-supplied", "মালিকের দেওয়া"), a.AnimalsBroughtInBdt);
        Add(assets, "", T("(−) Cost of animals sold / slaughtered", "(−) বিক্রিত / জবাইকৃত পশুর ক্রয়মূল্য"), T("Counted as a cost", "খরচ হিসেবে ধরা"), a.CostOfAnimalsSoldBdt);
        Add(assets, "", T("(−) Animals that died", "(−) মৃত পশু"), T("Counted as a cost (mortality loss)", "খরচ হিসেবে ধরা (মৃত্যুজনিত ক্ষতি)"), a.MortalityLossBdt);
        Add(assets, "1300", T("Animals at the end (at cost)", "শেষে পশু (ক্রয়মূল্যে)"), "", a.LivestockClosingBdt);

        // Credit book (receivables & payables)
        if (d.Credit is { } c)
        {
            var credit = Section("Credit: customers owe us / we owe suppliers", "বাকি: ক্রেতার কাছে পাওনা / সরবরাহকারীর দেনা");
            Add(credit, "1100", T("Customers owed us at the start", "শুরুতে ক্রেতাদের কাছে পাওনা"), T("Receivable", "প্রাপ্য"), c.ReceivablesOpeningBdt);
            Add(credit, "", T("(+) Sold on credit", "(+) বাকিতে বিক্রি"), "", c.CreditSalesBdt);
            Add(credit, "", T("(−) Collected from customers", "(−) ক্রেতার কাছ থেকে আদায়"), T("Cash in", "নগদ প্রাপ্তি"), c.CollectedBdt);
            Add(credit, "", T("(−) Bad debts written off", "(−) অনাদায়ী পাওনা বাতিল"), T("Counted as a cost", "খরচ হিসেবে ধরা"), c.BadDebtsBdt);
            Add(credit, "1100", T("Customers owe us at the end", "শেষে ক্রেতাদের কাছে পাওনা"), "", c.ReceivablesClosingBdt);
            Add(credit, "2100", T("We owed suppliers at the start", "শুরুতে সরবরাহকারীদের দেনা"), T("Payable", "প্রদেয়"), c.PayablesOpeningBdt);
            Add(credit, "", T("(+) Bought on credit", "(+) বাকিতে ক্রয়"), "", c.CreditPurchasesBdt);
            Add(credit, "", T("(−) Paid to suppliers", "(−) সরবরাহকারীকে পরিশোধ"), T("Cash out", "নগদ প্রদান"), c.PaidToSuppliersBdt);
            Add(credit, "", T("(−) Returns & waivers", "(−) ফেরত ও মওকুফ"), T("Taken off the bills", "বিল থেকে বাদ"), c.ReturnsAndWaiversBdt);
            Add(credit, "2100", T("We owe suppliers at the end", "শেষে সরবরাহকারীদের দেনা"), "", c.PayablesClosingBdt);
        }

        // Capital & financing
        var capital = Section("Owner, investor & loan money (cash)", "মালিক, বিনিয়োগকারী ও ঋণের অর্থ (নগদ)");
        Add(capital, "3010", T("Investor money received", "বিনিয়োগকারীর অর্থ প্রাপ্তি"), "", d.Capital.InvestorCapitalInBdt);
        Add(capital, "3010", T("Investor money withdrawn", "বিনিয়োগকারীর অর্থ উত্তোলন"), "", d.Capital.InvestorWithdrawalBdt);
        Add(capital, "3110", T("Profit paid to investors", "বিনিয়োগকারীদের দেওয়া মুনাফা"), "", d.Capital.ProfitDistributedBdt);
        Add(capital, "3200", T("Owner's money put in", "মালিকের দেওয়া অর্থ"), "", d.Capital.OwnerCapitalInBdt);
        Add(capital, "3210", T("Owner's drawings (taken out)", "মালিকের উত্তোলন"), "", d.Capital.OwnerDrawingBdt);
        Add(capital, "2010", T("Loan received", "ঋণ গ্রহণ"), "", d.Capital.LoanReceivedBdt);
        Add(capital, "2010", T("Loan principal repaid", "ঋণের আসল পরিশোধ"), "", d.Capital.LoanRepaidBdt);
        Add(capital, "5300", T("Loan interest paid", "ঋণের সুদ পরিশোধ"), T("A cost — already in farm costs", "খরচ — খামারের খরচে ধরা আছে"), d.Capital.LoanInterestPaidBdt);
        Add(capital, "", T("Net money in / (out)", "নিট অর্থ প্রাপ্তি / (প্রদান)"), T("Excludes interest", "সুদ বাদে"), d.Capital.NetCapitalFlowBdt);

        // Investors
        if (d.Investors.Count > 0)
        {
            var investors = Section("Investor accounts (capital at the end)", "বিনিয়োগকারীর হিসাব (শেষে মূলধন)");
            foreach (var i in d.Investors)
            {
                Add(investors, "", i.Name,
                    T(
                        $"Start {N0(i.OpeningCapitalBdt)} + in {N0(i.ContributedInPeriodBdt)} − out {N0(i.WithdrawnInPeriodBdt)} · profit share {P(i.ProfitSharePercentage)}% · {i.ShareCount} shares",
                        $"শুরু {N0(i.OpeningCapitalBdt)} + প্রদান {N0(i.ContributedInPeriodBdt)} − উত্তোলন {N0(i.WithdrawnInPeriodBdt)} · মুনাফার অংশ {P(i.ProfitSharePercentage)}% · {i.ShareCount}টি শেয়ার"),
                    i.ClosingCapitalBdt);
            }
        }

        // Profit sharing
        var profit = Section("Profit sharing", "মুনাফা বণ্টন");
        Add(profit, "", T("Net operating profit", "নিট পরিচালন মুনাফা"), T("From the farm costs section", "খামারের খরচ অংশ থেকে"), d.ProfitShare.NetOperatingProfitBdt);
        Add(profit, "", T("(−) Charity reserve", "(−) দাতব্য সঞ্চিতি"), T($"{P(d.ProfitShare.CharityPercentage)}% of profit (when there is a profit)", $"মুনাফার {P(d.ProfitShare.CharityPercentage)}% (মুনাফা হলে)"), d.ProfitShare.CharityAmountBdt);
        Add(profit, "", T("Profit available to share", "বণ্টনযোগ্য মুনাফা"), "", d.ProfitShare.DistributableProfitBdt);
        foreach (var i in d.Investors)
        {
            if (i.ProfitEntitledInPeriodBdt == 0 && i.ProfitPaidInPeriodBdt == 0)
                continue;
            Add(profit, "", $"  {i.Name}",
                T(
                    $"{P(i.ProfitSharePercentage)}% · paid {N2(i.ProfitPaidInPeriodBdt)} · still to pay {N2(i.ProfitPayableBdt)}",
                    $"{P(i.ProfitSharePercentage)}% · পরিশোধিত {N2(i.ProfitPaidInPeriodBdt)} · পরিশোধ বাকি {N2(i.ProfitPayableBdt)}"),
                i.ProfitEntitledInPeriodBdt);
        }

        Add(profit, "", T("Total due to investors", "বিনিয়োগকারীদের মোট প্রাপ্য"), "", d.ProfitShare.TotalEntitledBdt);
        Add(profit, "", T("Paid out in the period", "এই সময়ে পরিশোধিত"), "", d.ProfitShare.DistributedInPeriodBdt);
        Add(profit, "", T("Kept by the farm", "খামারে রাখা"), T("Available to share − due to investors", "বণ্টনযোগ্য − বিনিয়োগকারীদের প্রাপ্য"), d.ProfitShare.RetainedProfitBdt);

        // Shares
        var shares = Section("Farm shares", "খামারের শেয়ার");
        if (d.Shares.IsConfigured)
        {
            Add(shares, "", T("Farm value (all shares)", "খামারের মূল্য (সব শেয়ার)"), T($"{N0(d.Shares.TotalShares)} shares × {N2(d.Shares.SharePriceBdt)}", $"{N0(d.Shares.TotalShares)}টি শেয়ার × {N2(d.Shares.SharePriceBdt)}"), d.Shares.TotalValuationBdt);
            Add(shares, "", T("Shares kept by the owner", "মালিকের হাতে শেয়ার"), T($"{N0(d.Shares.OwnerShareCount)} shares · {P(d.Shares.OwnerOwnershipPercentage)}%", $"{N0(d.Shares.OwnerShareCount)}টি শেয়ার · {P(d.Shares.OwnerOwnershipPercentage)}%"), d.Shares.OwnerShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", T("Shares held by investors", "বিনিয়োগকারীদের হাতে শেয়ার"), T($"{N0(d.Shares.AllocatedShareCount)} shares", $"{N0(d.Shares.AllocatedShareCount)}টি শেয়ার"), d.Shares.AllocatedShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", T("Shares not yet issued", "এখনো ইস্যু না হওয়া শেয়ার"), T($"{N0(d.Shares.AvailableShareCount)} shares", $"{N0(d.Shares.AvailableShareCount)}টি শেয়ার"), d.Shares.AvailableShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", T("Shares sold in the period", "এই সময়ে বিক্রিত শেয়ার"), T($"{N0(d.Shares.SharesIssuedInPeriod)} shares", $"{N0(d.Shares.SharesIssuedInPeriod)}টি শেয়ার"), d.Shares.ShareSalesAmountBdt);
            Add(shares, "", T("Shares bought back", "ফেরত কেনা শেয়ার"), T($"{N0(d.Shares.SharesBoughtBackInPeriod)} shares", $"{N0(d.Shares.SharesBoughtBackInPeriod)}টি শেয়ার"), d.Shares.BuybackAmountBdt);
            Add(shares, "", T("Shares transferred", "হস্তান্তরিত শেয়ার"), T($"{N0(d.Shares.SharesTransferredInPeriod)} shares between investors", $"বিনিয়োগকারীদের মধ্যে {N0(d.Shares.SharesTransferredInPeriod)}টি শেয়ার"), null);
        }
        else
        {
            Add(shares, "", T("Farm shares not set up", "খামারের শেয়ার সেটআপ করা হয়নি"), T("Set up shares on the Share Market page", "শেয়ার মার্কেট পাতায় শেয়ার সেটআপ করুন"), null);
        }

        // Loans
        var loans = Section("Loans (principal still owed)", "ঋণ (পরিশোধযোগ্য আসল)");
        foreach (var l in d.Loans.Loans)
        {
            Add(loans, "", l.LenderName,
                T(
                    $"Borrowed {N0(l.PrincipalAmountBdt)} @ {P(l.InterestRatePercent)}% · principal repaid {N0(l.PrincipalRepaidBdt)}, interest paid {N0(l.InterestPaidBdt)} (this period {N0(l.RepaidInPeriodBdt)}) · still owed incl. interest {N0(l.OutstandingBdt)}",
                    $"ঋণ {N0(l.PrincipalAmountBdt)} @ {P(l.InterestRatePercent)}% · আসল পরিশোধ {N0(l.PrincipalRepaidBdt)}, সুদ পরিশোধ {N0(l.InterestPaidBdt)} (এই সময়ে {N0(l.RepaidInPeriodBdt)}) · সুদসহ বাকি {N0(l.OutstandingBdt)}"),
                l.OutstandingPrincipalBdt);
        }

        Add(loans, "2010", T("Total principal still owed", "মোট পরিশোধযোগ্য আসল"),
            T(
                $"{d.Loans.ActiveLoanCount} active loan(s); incl. interest still to pay: {N2(d.Loans.TotalOutstandingBdt)}",
                $"{d.Loans.ActiveLoanCount}টি চলমান ঋণ; সুদসহ মোট বাকি: {N2(d.Loans.TotalOutstandingBdt)}"),
            d.Loans.Loans.Sum(l => l.OutstandingPrincipalBdt));

        return rows;
    }

    // The subject line and the body both need the statement; load it once per run.
    private static Task<FullAccountsReportDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("full-accounts", () => LoadCoreAsync(context, cancellationToken));

    private static async Task<FullAccountsReportDto> LoadCoreAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var range = context.Range("period");
        return await context.Send(
            new GetFullAccountsReportQuery(
                context.FarmId(),
                range.From.ToDateTime(TimeOnly.MinValue),
                range.To.ToDateTime(TimeOnly.MinValue)),
            cancellationToken).ConfigureAwait(false);
    }
}
