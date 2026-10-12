using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetBalanceSheetQuery(
    Guid FarmId,
    DateTime? AsOfDate = null
) : IRequest<BalanceSheetDto>;

/// <summary>
/// Statement of financial position built from the double-entry journal of every posting up to the
/// date: Cash, Inventory (stock at cost) and Livestock (herd at cost) as assets; Loans Payable as
/// a liability; investor and owner capital plus retained earnings as equity.
/// </summary>
/// <remarks>
/// Because every posting is a complete double entry, Assets = Liabilities + Equity always holds.
/// Whether the books are <i>right</i> is shown by the reconciliations: each account is checked
/// against its own register (stock on hand, the herd, the loan register, the investor register).
/// </remarks>
public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAccountingReconciliationService _reconciliation;

    public GetBalanceSheetQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        IAccountingReconciliationService reconciliation)
    {
        _transactionRepository = transactionRepository;
        _reconciliation = reconciliation;
    }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateTime.UtcNow;
        var cutoff = asOf.Date.AddDays(1);

        var all = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var postings = all.Where(t => t.TransactionDate < cutoff).ToList();
        var balances = AccountChart.Balances(postings);
        decimal Bal(string code) => balances.GetValueOrDefault(code);

        // Profit: revenue and expense accounts are credit/debit balances, net profit = −Σ.
        var cumulativeProfit = AccountChart.NetProfit(postings);
        var yearStart = new DateTime(asOf.Year, 1, 1, 0, 0, 0, asOf.Kind);
        var currentYearProfit = AccountChart.NetProfit(postings.Where(t => t.TransactionDate >= yearStart));

        // ── Assets ──
        var cash = Bal(AccountChart.CashAccount);
        var inventory = Bal(AccountChart.InventoryAccount);
        var livestock = Bal(AccountChart.LivestockAccount);
        var receivable = Bal(AccountChart.ReceivableAccount);
        var payable = -Bal(AccountChart.PayableAccount);

        var assetLines = new List<BalanceSheetLineDto>
        {
            new(AccountChart.CashAccount, "Cash & Bank", R(Math.Max(0, cash)),
                cash >= 0 ? "Money received less money paid, from the cash book" : "Overdrawn — shown under liabilities"),
            new(AccountChart.ReceivableAccount, "Accounts Receivable (customers)", R(Math.Max(0, receivable)),
                "Credit sales not yet collected"),
            new(AccountChart.InventoryAccount, "Inventory (Feed, Medicine & Supplies)", R(inventory),
                "Stock bought or brought in, less stock used, returned or written off — at cost"),
            new(AccountChart.LivestockAccount, "Livestock (at purchase cost)", R(livestock),
                "Animals bought, less the cost of animals sold, slaughtered or dead"),
        };
        if (payable < 0)
        {
            assetLines.Add(new BalanceSheetLineDto("1150", "Advances to Suppliers", R(-payable),
                "Paid to suppliers beyond what their bills now come to — to be recovered"));
        }

        var totalAssets = assetLines.Sum(l => l.AmountBdt);

        // ── Liabilities ──
        var loans = -Bal(AccountChart.LoanPayableAccount);
        var liabilityLines = new List<BalanceSheetLineDto>
        {
            new(AccountChart.PayableAccount, "Accounts Payable (suppliers)", R(Math.Max(0, payable)),
                "Bought on credit and not yet paid"),
            new(AccountChart.LoanPayableAccount, "Loans Payable (principal)", R(loans),
                "Loans received less principal repaid (interest is an expense, not debt reduction)"),
        };
        if (receivable < 0)
        {
            liabilityLines.Add(new BalanceSheetLineDto("2150", "Customer Advances", R(-receivable),
                "Collected from customers beyond what their invoices now come to — owed back or to be delivered"));
        }
        if (cash < 0)
        {
            liabilityLines.Add(new BalanceSheetLineDto("2020", "Bank Overdraft / Cash Deficit", R(-cash),
                "More recorded as paid than received — check for a missing opening balance or capital receipt"));
        }

        var totalLiabilities = liabilityLines.Sum(l => l.AmountBdt);

        // ── Equity ──
        var investorCapital = -Bal(AccountChart.InvestorCapitalAccount);
        var distributed = Bal(AccountChart.ProfitDistributionAccount);
        var ownerCapital = -Bal(AccountChart.OwnerCapitalAccount);
        var drawings = Bal(AccountChart.OwnerDrawingAccount);

        var equityLines = new List<BalanceSheetLineDto>
        {
            new(AccountChart.InvestorCapitalAccount, "Investor Capital", R(investorCapital), "Capital invested less capital withdrawn"),
            new(AccountChart.OwnerCapitalAccount, "Owner's Capital", R(ownerCapital), "Owner's cash, opening stock and animals brought in"),
            new(AccountChart.OwnerDrawingAccount, "Less: Owner's Drawings", R(-drawings), "Cash taken out by the owner"),
            new(AccountChart.RetainedEarningsAccount, "Retained Earnings (cumulative profit)", R(cumulativeProfit),
                $"All profit and loss to date, of which {currentYearProfit:N2} BDT this year"),
            new(AccountChart.ProfitDistributionAccount, "Less: Profit Distributed to Investors", R(-distributed), "Profit already paid out"),
        };
        var totalEquity = equityLines.Sum(l => l.AmountBdt);

        var totalLiabilitiesAndEquity = R(totalLiabilities + totalEquity);
        var difference = R(Math.Abs(totalAssets - totalLiabilitiesAndEquity));

        var reconciliations = await _reconciliation.ReconcileAsync(request.FarmId, all, cancellationToken);

        return new BalanceSheetDto(
            request.FarmId,
            asOf,
            new BalanceSheetSectionDto("Assets", R(totalAssets), assetLines),
            new BalanceSheetSectionDto("Liabilities", R(totalLiabilities), liabilityLines),
            new BalanceSheetSectionDto("Equity", R(totalEquity), equityLines),
            R(totalAssets),
            totalLiabilitiesAndEquity,
            difference == 0m,
            difference,
            R(Math.Max(0, cash) + Math.Max(0, receivable) + inventory - totalLiabilities))
        {
            CurrentYearProfitBdt = R(currentYearProfit),
            // Registers are current snapshots, so the comparison is only meaningful for today.
            Reconciliations = asOf.Date >= DateTime.UtcNow.Date ? reconciliations : [],
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 2);
}
