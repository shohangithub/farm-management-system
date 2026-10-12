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

public record GetInvestorPnLQuery(
    Guid FarmId,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<InvestorPnLSummaryDto>;

public class GetInvestorPnLQueryHandler : IRequestHandler<GetInvestorPnLQuery, InvestorPnLSummaryDto>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IInvestorRepository _investorRepository;

    public GetInvestorPnLQueryHandler(
        IFinancialTransactionRepository transactionRepository,
        IInvestorRepository investorRepository)
    {
        _transactionRepository = transactionRepository;
        _investorRepository = investorRepository;
    }

    public async Task<InvestorPnLSummaryDto> Handle(GetInvestorPnLQuery request, CancellationToken cancellationToken)
    {
        var allTx = await _transactionRepository.GetAllByFarmIdAsync(request.FarmId, cancellationToken);

        if (request.FromDate.HasValue)
            allTx = allTx.Where(t => t.TransactionDate >= request.FromDate.Value).ToList();

        if (request.ToDate.HasValue)
            allTx = allTx.Where(t => t.TransactionDate <= request.ToDate.Value).ToList();

        // Calculate Operating Revenue (exclude capital injections and loan disbursements)
        var operatingRevenue = allTx
            .Where(t => t.Type == TransactionType.Income 
                        && t.Category != TransactionCategory.InvestorCapital 
                        && t.Category != TransactionCategory.LoanDisbursement)
            .Sum(t => t.AmountBdt);

        // Calculate Operating Expenses (exclude capital withdrawals, profit payouts, loan principal repayments)
        var operatingExpenses = allTx
            .Where(t => t.Type == TransactionType.Expense 
                        && t.Category != TransactionCategory.InvestorWithdrawal 
                        && t.Category != TransactionCategory.ProfitDistribution 
                        && t.Category != TransactionCategory.LoanRepayment)
            .Sum(t => t.AmountBdt);

        var netFarmProfit = operatingRevenue - operatingExpenses;

        // 5% goes to charity if net profit is positive; rest is distributable to investors
        const decimal charityPercentage = 5.0m;
        decimal charityAmount = netFarmProfit > 0 ? Math.Round(netFarmProfit * (charityPercentage / 100m), 2) : 0m;
        decimal distributableProfit = netFarmProfit - charityAmount;

        var investors = await _investorRepository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);
        var activeInvestors = investors.Where(i => i.IsActive).ToList();

        var totalActiveCapital = activeInvestors.Sum(i => i.CurrentCapitalBdt);

        // Agreed percentages and capital-based shares must never co-exist on the SAME 100% pool --
        // an investor with no explicit agreement should only draw from whatever percentage the
        // agreed investors haven't already claimed, split by capital among the other non-agreed
        // investors. Previously every non-agreed investor's share was computed against TOTAL
        // active capital regardless of what agreed investors already took, so e.g. one investor
        // agreed at 80% plus another defaulting to 50% of total capital allocated 130% of the
        // farm's distributable profit -- more than existed.
        decimal totalAgreedPct = activeInvestors
            .Where(i => i.AgreedProfitSharePercentage.HasValue)
            .Sum(i => i.AgreedProfitSharePercentage!.Value);
        decimal remainingPctForNonAgreed = Math.Max(0m, 100m - totalAgreedPct);
        decimal totalNonAgreedCapital = activeInvestors
            .Where(i => !i.AgreedProfitSharePercentage.HasValue)
            .Sum(i => i.CurrentCapitalBdt);

        var investorShares = new List<InvestorShareDto>();
        decimal totalDistributedProfit = 0m;
        decimal totalUndistributedProfit = 0m;

        foreach (var inv in investors)
        {
            decimal capitalSharePct = totalActiveCapital > 0 && inv.IsActive
                ? Math.Round((inv.CurrentCapitalBdt / totalActiveCapital) * 100m, 2)
                : 0m;

            decimal effectiveSharePct;
            if (inv.AgreedProfitSharePercentage.HasValue)
            {
                effectiveSharePct = inv.AgreedProfitSharePercentage.Value;
            }
            else if (inv.IsActive && totalNonAgreedCapital > 0)
            {
                effectiveSharePct = Math.Round((inv.CurrentCapitalBdt / totalNonAgreedCapital) * remainingPctForNonAgreed, 2);
            }
            else
            {
                effectiveSharePct = 0m;
            }

            // Profit sharing is based on distributable profit (after charity deduction)
            decimal allocatedProfit = Math.Round(distributableProfit * (effectiveSharePct / 100m), 2);
            decimal distributedProfit = inv.TotalProfitPaidBdt;
            decimal undistributedProfit = allocatedProfit - distributedProfit;
            decimal netEquityValue = inv.CurrentCapitalBdt + undistributedProfit;

            totalDistributedProfit += distributedProfit;
            totalUndistributedProfit += undistributedProfit;

            investorShares.Add(new InvestorShareDto(
                inv.Id,
                inv.Name,
                inv.CurrentCapitalBdt,
                capitalSharePct,
                effectiveSharePct,
                allocatedProfit,
                distributedProfit,
                undistributedProfit,
                netEquityValue,
                inv.IsActive
            ));
        }

        return new InvestorPnLSummaryDto(
            request.FarmId,
            request.FromDate,
            request.ToDate,
            Math.Round(operatingRevenue, 2),
            Math.Round(operatingExpenses, 2),
            Math.Round(netFarmProfit, 2),
            charityPercentage,
            charityAmount,
            Math.Round(distributableProfit, 2),
            Math.Round(totalActiveCapital, 2),
            Math.Round(totalDistributedProfit, 2),
            Math.Round(totalUndistributedProfit, 2),
            investorShares
        );
    }
}
