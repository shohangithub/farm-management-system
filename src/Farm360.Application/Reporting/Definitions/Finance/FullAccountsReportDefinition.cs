using System;
using System.Collections.Generic;
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
/// investor capital, profit sharing, share capital and loans on one printable statement.
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
        "Everything in one statement: cash, income, all costs incl. labour, investment, share capital, profit share and loans.",
        "এক বিবরণীতে সব: নগদ, আয়, শ্রমিকসহ সকল খরচ, বিনিয়োগ, শেয়ার, লভ্যাংশ ও ঋণ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.DateRange("period", @default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<FullAccountsReportRow>> Columns =>
    [
        ReportColumn<FullAccountsReportRow>.Text("code", new LocalizedText("Code", "কোড"), r => r.Code, width: 16f, relative: false),
        ReportColumn<FullAccountsReportRow>.Text("item", new LocalizedText("Line Item", "খাত"), r => r.LineItem, width: 2.6f),
        ReportColumn<FullAccountsReportRow>.Text("notes", new LocalizedText("Notes / Basis", "নোট / ভিত্তি"), r => r.Notes, width: 2.4f),
        ReportColumn<FullAccountsReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, widthMm: 30f, aggregate: ReportAggregate.None),
    ];

    public override ReportGroup<FullAccountsReportRow>? Group =>
        ReportGroup<FullAccountsReportRow>.By(r => r.Section, r => r.Section);

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult<string?>($"Accounts for {context.Range("period")}");
    }

    protected override async Task<IReadOnlyList<FullAccountsReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await FinanceReportFarm.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
        var range = context.Range("period");
        var d = await context.Send(
            new GetFullAccountsReportQuery(
                farmId,
                range.From.ToDateTime(TimeOnly.MinValue),
                range.To.ToDateTime(TimeOnly.MinValue)),
            cancellationToken).ConfigureAwait(false);

        var rows = new List<FullAccountsReportRow>();
        void Add(string section, string code, string item, string notes, decimal? amount) =>
            rows.Add(new FullAccountsReportRow(section, code, item, notes, amount));

        // 1. Cash
        const string cash = "1. Cash Position";
        Add(cash, "1010", "Opening Cash Balance", $"As of {d.FromDate:dd-MMM-yyyy}", d.Cash.OpeningBalanceBdt);
        Add(cash, "", "(+) Total Inflow", $"{d.Cash.TransactionCount} transaction(s) in period", d.Cash.TotalInflowBdt);
        Add(cash, "", "(-) Total Outflow", "", d.Cash.TotalOutflowBdt);
        Add(cash, "", "Net Cash Flow", "", d.Cash.NetCashFlowBdt);
        Add(cash, "1010", "Closing Cash Balance", $"As of {d.ToDate:dd-MMM-yyyy}", d.Cash.ClosingBalanceBdt);

        // 2. Operating income
        const string revenue = "2. Operating Income";
        foreach (var l in d.Operating.RevenueLines)
            Add(revenue, l.Code, l.Name, $"{l.Count} entr(ies) · {l.PercentOfTotal:0.#}%", l.TotalBdt);
        Add(revenue, "", "Total Operating Income", "", d.Operating.TotalRevenueBdt);

        // 3. Operating expenses
        const string expense = "3. Operating Expenses";
        foreach (var l in d.Operating.ExpenseLines)
            Add(expense, l.Code, l.Name, $"{l.Count} entr(ies) · {l.PercentOfTotal:0.#}%", l.TotalBdt);
        Add(expense, "", "Total Operating Expenses", "", d.Operating.TotalExpenseBdt);
        Add(expense, "", d.Operating.NetOperatingProfitBdt >= 0 ? "NET OPERATING PROFIT" : "NET OPERATING LOSS",
            $"Margin {d.Operating.ProfitMarginPercent:0.##}%", d.Operating.NetOperatingProfitBdt);

        // 4. Cost analysis
        const string costs = "4. Cost Analysis";
        foreach (var g in d.Costs.Groups)
            Add(costs, "", g.Name, $"{g.Count} entr(ies) · {g.PercentOfTotal:0.#}% of cost", g.AmountBdt);
        Add(costs, "", "Average Daily Cost", $"Over {d.Days} day(s)", d.Costs.AverageDailyCostBdt);
        Add(costs, "", "Automated Postings", "Feeding, inventory & livestock modules", d.Costs.AutomatedPostingsBdt);
        Add(costs, "", "Manual Postings", "Entered by staff", d.Costs.ManualPostingsBdt);

        // 5. Stock & livestock (non-cash assets)
        const string assets = "5. Stock & Livestock (assets at cost)";
        var a = d.Assets;
        Add(assets, "1200", "Inventory - opening", "", a.InventoryOpeningBdt);
        Add(assets, "", "(+) Stock purchased", "Paid in cash or bought on credit", a.StockPurchasedBdt);
        Add(assets, "", "(+) Stock brought in", "Opening / owner-supplied stock", a.StockBroughtInBdt);
        Add(assets, "", "(-) Returned to suppliers", "", a.StockReturnedBdt);
        Add(assets, "", "(-) Used (feed, medicine, consumables)", "Charged to expenses", a.StockConsumedBdt);
        Add(assets, "", "(-) Written off", "Damaged / expired / shrinkage", a.StockWrittenOffBdt);
        Add(assets, "1200", "Inventory - closing", "", a.InventoryClosingBdt);
        Add(assets, "1300", "Livestock - opening", "", a.LivestockOpeningBdt);
        Add(assets, "", "(+) Animals purchased", "", a.AnimalsPurchasedBdt);
        Add(assets, "", "(+) Animals brought in", "Owner-supplied", a.AnimalsBroughtInBdt);
        Add(assets, "", "(-) Cost of animals sold / slaughtered", "Charged to expenses", a.CostOfAnimalsSoldBdt);
        Add(assets, "", "(-) Mortality loss", "Charged to expenses", a.MortalityLossBdt);
        Add(assets, "1300", "Livestock - closing", "", a.LivestockClosingBdt);

        // 5b. Credit book (payables & receivables)
        if (d.Credit is { } c)
        {
            const string credit = "5b. Receivables & Payables (credit)";
            Add(credit, "1100", "Receivables - opening", "Owed by customers", c.ReceivablesOpeningBdt);
            Add(credit, "", "(+) Sold on credit", "", c.CreditSalesBdt);
            Add(credit, "", "(-) Collected from customers", "Cash in", c.CollectedBdt);
            Add(credit, "", "(-) Bad debts written off", "Charged to expenses", c.BadDebtsBdt);
            Add(credit, "1100", "Receivables - closing", "", c.ReceivablesClosingBdt);
            Add(credit, "2100", "Payables - opening", "Owed to suppliers", c.PayablesOpeningBdt);
            Add(credit, "", "(+) Bought on credit", "", c.CreditPurchasesBdt);
            Add(credit, "", "(-) Paid to suppliers", "Cash out", c.PaidToSuppliersBdt);
            Add(credit, "", "(-) Returns & waivers", "Deducted from bills", c.ReturnsAndWaiversBdt);
            Add(credit, "2100", "Payables - closing", "", c.PayablesClosingBdt);
        }

        // 5b. Capital & financing
        const string capital = "6. Investment & Financing (Cash)";
        Add(capital, "3010", "Investor Capital Received", "", d.Capital.InvestorCapitalInBdt);
        Add(capital, "3010", "Investor Capital Withdrawn", "", d.Capital.InvestorWithdrawalBdt);
        Add(capital, "3110", "Profit Distributed", "", d.Capital.ProfitDistributedBdt);
        Add(capital, "3200", "Owner's Capital Introduced", "", d.Capital.OwnerCapitalInBdt);
        Add(capital, "3210", "Owner's Drawings", "", d.Capital.OwnerDrawingBdt);
        Add(capital, "2010", "Loan Received", "", d.Capital.LoanReceivedBdt);
        Add(capital, "2010", "Loan Principal Repaid", "", d.Capital.LoanRepaidBdt);
        Add(capital, "5300", "Loan Interest Paid", "Expense (included in P&L)", d.Capital.LoanInterestPaidBdt);
        Add(capital, "", "Net Capital Flow", "Excludes interest", d.Capital.NetCapitalFlowBdt);

        // 6. Investors
        const string investors = "7. Investor Accounts";
        foreach (var i in d.Investors)
        {
            Add(investors, "", i.Name,
                $"Opening {i.OpeningCapitalBdt:N0} + In {i.ContributedInPeriodBdt:N0} - Out {i.WithdrawnInPeriodBdt:N0} · " +
                $"Profit share {i.ProfitSharePercentage:0.##}% · Shares {i.ShareCount}",
                i.ClosingCapitalBdt);
        }

        // 7. Profit sharing
        const string profit = "8. Profit Sharing";
        Add(profit, "", "Net Operating Profit", "", d.ProfitShare.NetOperatingProfitBdt);
        Add(profit, "", "Charity Reserve", $"{d.ProfitShare.CharityPercentage:0.##}% of positive profit", d.ProfitShare.CharityAmountBdt);
        Add(profit, "", "Distributable Profit", "", d.ProfitShare.DistributableProfitBdt);
        foreach (var i in d.Investors)
        {
            if (i.ProfitEntitledInPeriodBdt == 0 && i.ProfitPaidInPeriodBdt == 0)
                continue;
            Add(profit, "", $"  {i.Name}",
                $"{i.ProfitSharePercentage:0.##}% · paid {i.ProfitPaidInPeriodBdt:N2} · payable {i.ProfitPayableBdt:N2}",
                i.ProfitEntitledInPeriodBdt);
        }

        Add(profit, "", "Total Investor Entitlement", "", d.ProfitShare.TotalEntitledBdt);
        Add(profit, "", "Distributed in Period", "", d.ProfitShare.DistributedInPeriodBdt);
        Add(profit, "", "Retained by Farm", "Distributable less investor entitlement", d.ProfitShare.RetainedProfitBdt);

        // 8. Shares
        const string shares = "9. Share Capital";
        if (d.Shares.IsConfigured)
        {
            Add(shares, "", "Farm Valuation", $"{d.Shares.TotalShares:N0} shares x {d.Shares.SharePriceBdt:N2}", d.Shares.TotalValuationBdt);
            Add(shares, "", "Owner Retained Shares", $"{d.Shares.OwnerShareCount:N0} shares · {d.Shares.OwnerOwnershipPercentage:0.##}%", d.Shares.OwnerShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", "Investor-held Shares", $"{d.Shares.AllocatedShareCount:N0} shares", d.Shares.AllocatedShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", "Unissued Shares", $"{d.Shares.AvailableShareCount:N0} shares", d.Shares.AvailableShareCount * d.Shares.SharePriceBdt);
            Add(shares, "", "Shares Sold in Period", $"{d.Shares.SharesIssuedInPeriod:N0} shares", d.Shares.ShareSalesAmountBdt);
            Add(shares, "", "Shares Bought Back", $"{d.Shares.SharesBoughtBackInPeriod:N0} shares", d.Shares.BuybackAmountBdt);
            Add(shares, "", "Shares Transferred", $"{d.Shares.SharesTransferredInPeriod:N0} shares between investors", null);
        }
        else
        {
            Add(shares, "", "Share capital not configured", "Configure shares on the Share Market page", null);
        }

        // 9. Loans
        const string loans = "10. Loans";
        foreach (var l in d.Loans.Loans)
        {
            Add(loans, "", l.LenderName,
                $"Principal {l.PrincipalAmountBdt:N0} @ {l.InterestRatePercent:0.##}% · principal repaid {l.PrincipalRepaidBdt:N0}, interest paid {l.InterestPaidBdt:N0} (in period {l.RepaidInPeriodBdt:N0}) · total still owed {l.OutstandingBdt:N0}",
                l.OutstandingPrincipalBdt);
        }

        Add(loans, "2010", "Principal Outstanding", $"{d.Loans.ActiveLoanCount} active loan(s); incl. interest still payable: {d.Loans.TotalOutstandingBdt:N2}",
            d.Loans.Loans.Sum(l => l.OutstandingPrincipalBdt));

        return rows;
    }
}
