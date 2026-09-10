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

public record GetTrialBalanceQuery(
    Guid FarmId,
    DateTime? AsOfDate = null
) : IRequest<TrialBalanceDto>;

public class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, TrialBalanceDto>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly ILoanRecordRepository _loanRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetTrialBalanceQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        ILoanRecordRepository loanRepository,
        IInvestorRepository investorRepository)
    {
        _transactionRepository = transactionRepository;
        _loanRepository = loanRepository;
        _investorRepository = investorRepository;
    }

    public async Task<TrialBalanceDto> Handle(GetTrialBalanceQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateTime.UtcNow;

        var allTx = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var filteredTx = allTx.Where(t => t.TransactionDate <= asOf).ToList();

        var allLoans = await _loanRepository.GetByFarmIdAsync(request.FarmId, cancellationToken);
        var filteredLoans = allLoans.Where(l => l.DisbursementDate <= asOf).ToList();

        var allInvestors = await _investorRepository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);

        // 1. Operating Revenue
        var revenueCategories = new[]
        {
            (TransactionCategory.AnimalSale, "4010", "Livestock Sales Revenue"),
            (TransactionCategory.MilkSale, "4020", "Dairy & Milk Sales Revenue"),
            (TransactionCategory.ByproductSale, "4030", "Byproduct & Manure Sales Revenue"),
            (TransactionCategory.OtherIncome, "4040", "Other Farm Operating Income")
        };

        var revenueLines = new List<TrialBalanceLineDto>();
        decimal totalRevenue = 0m;
        foreach (var (cat, code, name) in revenueCategories)
        {
            var amount = filteredTx
                .Where(t => t.Type == TransactionType.Income && t.Category == cat)
                .Sum(t => t.AmountBdt);

            if (amount > 0)
            {
                revenueLines.Add(new TrialBalanceLineDto(code, name, "Revenue", 0m, Math.Round(amount, 2)));
                totalRevenue += amount;
            }
        }

        // 2. Operating Expenses
        var expenseCategories = new[]
        {
            (TransactionCategory.AnimalPurchase, "5010", "Livestock Purchases"),
            (TransactionCategory.FeedCost, "5020", "Feed & Nutrition Expenses"),
            (TransactionCategory.VeterinaryCost, "5030", "Veterinary & Healthcare"),
            (TransactionCategory.LaborCost, "5040", "Labor & Farm Wages"),
            (TransactionCategory.Utilities, "5050", "Utilities & Energy"),
            (TransactionCategory.Transport, "5060", "Transport & Logistics"),
            (TransactionCategory.MiscellaneousExpense, "5070", "Miscellaneous Farm Expenses"),
            (TransactionCategory.InventoryPurchase, "5080", "Inventory Supplies & Materials"),
            (TransactionCategory.MedicineCost, "5090", "Medicines & Vaccines"),
            (TransactionCategory.ConsumableExpense, "5100", "Consumables & Daily Supplies")
        };

        var expenseLines = new List<TrialBalanceLineDto>();
        decimal totalExpenses = 0m;
        foreach (var (cat, code, name) in expenseCategories)
        {
            var amount = filteredTx
                .Where(t => t.Type == TransactionType.Expense && t.Category == cat)
                .Sum(t => t.AmountBdt);

            if (amount > 0)
            {
                expenseLines.Add(new TrialBalanceLineDto(code, name, "Expense", Math.Round(amount, 2), 0m));
                totalExpenses += amount;
            }
        }

        // 3. Loans Payable (Liabilities)
        decimal totalLoanDisbursements = filteredLoans.Sum(l => l.PrincipalAmountBdt);
        decimal totalLoanRepaid = filteredLoans.Sum(l => l.TotalRepaidBdt);
        decimal outstandingLoanPrincipal = Math.Max(0m, totalLoanDisbursements - totalLoanRepaid);

        var liabilityLines = new List<TrialBalanceLineDto>();
        if (outstandingLoanPrincipal > 0)
        {
            liabilityLines.Add(new TrialBalanceLineDto(
                "2010",
                "Loans Payable (Outstanding Principal)",
                "Liability",
                0m,
                Math.Round(outstandingLoanPrincipal, 2)
            ));
        }

        // 4. Investor Capital & Profit Distributions (Equity)
        decimal totalInvestorCapital = 0m;
        decimal totalProfitDistributed = 0m;

        foreach (var inv in allInvestors)
        {
            var txs = inv.Transactions?.Where(t => t.TransactionDate <= asOf).ToList() ?? new List<Domain.Finance.InvestorTransaction>();
            decimal invested = txs.Where(t => t.Type == InvestorTransactionType.CapitalContribution).Sum(t => t.AmountBdt);
            decimal withdrawn = txs.Where(t => t.Type == InvestorTransactionType.CapitalWithdrawal).Sum(t => t.AmountBdt);
            decimal profits = txs.Where(t => t.Type == InvestorTransactionType.ProfitDistribution).Sum(t => t.AmountBdt);

            // If no individual transactions found (e.g. initial setup only), fallback to investor entity totals
            if (txs.Count == 0 && inv.InvestmentDate <= asOf)
            {
                invested = inv.TotalInvestedBdt;
                withdrawn = inv.TotalWithdrawnBdt;
                profits = inv.TotalProfitPaidBdt;
            }

            totalInvestorCapital += (invested - withdrawn);
            totalProfitDistributed += profits;
        }

        var equityLines = new List<TrialBalanceLineDto>();
        if (totalInvestorCapital > 0)
        {
            equityLines.Add(new TrialBalanceLineDto(
                "3010",
                "Contributed Capital (Investors)",
                "Equity",
                0m,
                Math.Round(totalInvestorCapital, 2)
            ));
        }

        if (totalProfitDistributed > 0)
        {
            equityLines.Add(new TrialBalanceLineDto(
                "3020",
                "Profit Distributions Paid",
                "Equity",
                Math.Round(totalProfitDistributed, 2),
                0m
            ));
        }

        // 5. Cash & Cash Equivalents (Assets)
        // Option A derivation: Net Operating Profit + Outstanding Loans + Net Investor Capital - Profit Distributed
        decimal netCash = (totalRevenue - totalExpenses) + (totalLoanDisbursements - totalLoanRepaid) + (totalInvestorCapital - totalProfitDistributed);

        var assetLines = new List<TrialBalanceLineDto>();
        if (netCash >= 0)
        {
            assetLines.Add(new TrialBalanceLineDto(
                "1010",
                "Cash & Cash Equivalents",
                "Asset",
                Math.Round(netCash, 2),
                0m
            ));
        }
        else
        {
            assetLines.Add(new TrialBalanceLineDto(
                "1010",
                "Cash & Bank Overdraft",
                "Asset",
                0m,
                Math.Round(Math.Abs(netCash), 2)
            ));
        }

        var allLines = new List<TrialBalanceLineDto>();
        allLines.AddRange(assetLines);
        allLines.AddRange(liabilityLines);
        allLines.AddRange(equityLines);
        allLines.AddRange(revenueLines);
        allLines.AddRange(expenseLines);

        decimal totalDebit = allLines.Sum(l => l.DebitBdt);
        decimal totalCredit = allLines.Sum(l => l.CreditBdt);
        decimal difference = Math.Round(Math.Abs(totalDebit - totalCredit), 2);
        bool isBalanced = difference == 0m;

        return new TrialBalanceDto(
            request.FarmId,
            asOf,
            allLines,
            Math.Round(totalDebit, 2),
            Math.Round(totalCredit, 2),
            isBalanced,
            difference
        );
    }
}
