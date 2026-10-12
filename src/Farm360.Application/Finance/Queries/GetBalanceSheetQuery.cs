using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetBalanceSheetQuery(
    Guid FarmId,
    DateTime? AsOfDate = null
) : IRequest<BalanceSheetDto>;

public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly ILoanRecordRepository _loanRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetBalanceSheetQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        ILoanRecordRepository loanRepository,
        IInvestorRepository investorRepository)
    {
        _transactionRepository = transactionRepository;
        _loanRepository = loanRepository;
        _investorRepository = investorRepository;
    }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateTime.UtcNow;

        var allTx = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var filteredTx = allTx.Where(t => t.TransactionDate <= asOf).ToList();

        var allLoans = await _loanRepository.GetByFarmIdAsync(request.FarmId, cancellationToken);
        var filteredLoans = allLoans.Where(l => l.DisbursementDate <= asOf).ToList();

        var allInvestors = await _investorRepository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);

        // 1. Operational Totals
        decimal totalRevenue = filteredTx
            .Where(t => t.Type == TransactionType.Income 
                        && t.Category != TransactionCategory.InvestorCapital 
                        && t.Category != TransactionCategory.LoanDisbursement)
            .Sum(t => t.AmountBdt);

        decimal totalExpenses = filteredTx
            .Where(t => t.Type == TransactionType.Expense 
                        && t.Category != TransactionCategory.InvestorWithdrawal 
                        && t.Category != TransactionCategory.ProfitDistribution 
                        && t.Category != TransactionCategory.LoanRepayment)
            .Sum(t => t.AmountBdt);

        // 2. Loans
        decimal totalLoanDisbursements = filteredLoans.Sum(l => l.PrincipalAmountBdt);
        decimal totalLoanRepaid = filteredLoans.Sum(l => l.TotalRepaidBdt);
        decimal outstandingLoanPrincipal = Math.Max(0m, totalLoanDisbursements - totalLoanRepaid);

        // 3. Investors & Equity
        decimal totalInvestorCapital = 0m;
        decimal totalProfitDistributed = 0m;

        foreach (var inv in allInvestors)
        {
            var txs = inv.Transactions?.Where(t => t.TransactionDate <= asOf).ToList() ?? new List<Domain.Finance.InvestorTransaction>();
            decimal invested = txs.Where(t => t.Type == InvestorTransactionType.CapitalContribution).Sum(t => t.AmountBdt);
            decimal withdrawn = txs.Where(t => t.Type == InvestorTransactionType.CapitalWithdrawal).Sum(t => t.AmountBdt);
            decimal profits = txs.Where(t => t.Type == InvestorTransactionType.ProfitDistribution).Sum(t => t.AmountBdt);

            if (txs.Count == 0 && inv.InvestmentDate <= asOf)
            {
                invested = inv.TotalInvestedBdt;
                withdrawn = inv.TotalWithdrawnBdt;
                profits = inv.TotalProfitPaidBdt;
            }

            totalInvestorCapital += (invested - withdrawn);
            totalProfitDistributed += profits;
        }

        // 4. Retained Earnings (Cumulative Operating Profit minus Distributed Profits)
        decimal cumulativeOperatingProfit = totalRevenue - totalExpenses;
        decimal retainedEarnings = cumulativeOperatingProfit - totalProfitDistributed;

        // 5. Cash & Cash Equivalents -- summed directly from EVERY recorded transaction's cash
        // effect (no category exclusions), deliberately independent of the Liabilities/Equity
        // figures below (which come from LoanRecord/InvestorTransaction, not FinancialTransaction).
        // The old version derived "cash" algebraically from those same Liabilities/Equity inputs
        // (retainedEarnings + outstandingLoanPrincipal + totalInvestorCapital), which made
        // Assets == Liabilities+Equity true by construction for ANY data -- isBalanced could never
        // be false, even with a real bookkeeping error, because it was comparing a number to
        // itself. Pulling cash straight from the transaction ledger means a mismatch (e.g. a loan
        // disbursement posted here with no matching LoanRecord, or vice versa) now actually shows up.
        decimal actualCash = filteredTx.Sum(t => t.Type == TransactionType.Income ? t.AmountBdt : -t.AmountBdt);

        // Assets
        var assetLines = new List<BalanceSheetLineDto>();
        if (actualCash >= 0)
        {
            assetLines.Add(new BalanceSheetLineDto(
                "1010",
                "Cash & Cash Equivalents",
                Math.Round(actualCash, 2),
                "Net of all recorded income and expense transactions to date"
            ));
        }
        else
        {
            assetLines.Add(new BalanceSheetLineDto(
                "1010",
                "Cash & Cash Equivalents",
                0m,
                "Cash deficit funded via short-term overdraft liability"
            ));
        }

        decimal totalAssets = assetLines.Sum(l => l.AmountBdt);
        var assetsSection = new BalanceSheetSectionDto("Assets", Math.Round(totalAssets, 2), assetLines);

        // Liabilities
        var liabilityLines = new List<BalanceSheetLineDto>();
        liabilityLines.Add(new BalanceSheetLineDto(
            "2010",
            "Loans & Borrowings Payable",
            Math.Round(outstandingLoanPrincipal, 2),
            $"Outstanding balance across {filteredLoans.Count(l => l.IsActive)} active loan facility(ies)"
        ));

        if (actualCash < 0)
        {
            liabilityLines.Add(new BalanceSheetLineDto(
                "2020",
                "Bank Overdraft / Short-Term Facility",
                Math.Round(Math.Abs(actualCash), 2),
                "Operating deficit cash advance"
            ));
        }

        decimal totalLiabilities = liabilityLines.Sum(l => l.AmountBdt);
        var liabilitiesSection = new BalanceSheetSectionDto("Liabilities", Math.Round(totalLiabilities, 2), liabilityLines);

        // Equity
        var equityLines = new List<BalanceSheetLineDto>
        {
            new BalanceSheetLineDto(
                "3010",
                "Contributed Capital (Investors)",
                Math.Round(totalInvestorCapital, 2),
                $"Net capital contributed by {allInvestors.Count(i => i.IsActive)} active investor(s)"
            ),
            new BalanceSheetLineDto(
                "3020",
                "Retained Earnings",
                Math.Round(retainedEarnings, 2),
                $"Cumulative operational profit ({cumulativeOperatingProfit:N2} BDT) less profit distributions ({totalProfitDistributed:N2} BDT)"
            )
        };

        decimal totalEquity = equityLines.Sum(l => l.AmountBdt);
        var equitySection = new BalanceSheetSectionDto("Equity", Math.Round(totalEquity, 2), equityLines);

        decimal totalLiabilitiesAndEquity = Math.Round(totalLiabilities + totalEquity, 2);
        decimal difference = Math.Round(Math.Abs(totalAssets - totalLiabilitiesAndEquity), 2);
        bool isBalanced = difference == 0m;
        decimal netWorkingCapital = Math.Round(totalAssets - totalLiabilities, 2);

        return new BalanceSheetDto(
            request.FarmId,
            asOf,
            assetsSection,
            liabilitiesSection,
            equitySection,
            Math.Round(totalAssets, 2),
            totalLiabilitiesAndEquity,
            isBalanced,
            difference,
            netWorkingCapital
        );
    }
}
