using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Application.Finance.Services;
using MediatR;

namespace Farm360.Application.Finance.Queries;

public record GetInvestorPnLQuery(
    Guid FarmId,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<InvestorPnLSummaryDto>;

public class GetInvestorPnLQueryHandler : IRequestHandler<GetInvestorPnLQuery, InvestorPnLSummaryDto>
{
    private readonly IFinancialStatementEngine _engine;
    private readonly IInvestorRepository _investorRepository;

    public GetInvestorPnLQueryHandler(
        IFinancialStatementEngine engine,
        IInvestorRepository investorRepository)
    {
        _engine = engine;
        _investorRepository = investorRepository;
    }

    public async Task<InvestorPnLSummaryDto> Handle(GetInvestorPnLQuery request, CancellationToken cancellationToken)
    {
        // Whole days, both ends inclusive: a posting at 18:00 on the last day belongs to the period.
        var fromDay = request.FromDate.HasValue ? DateOnly.FromDateTime(request.FromDate.Value) : DateOnly.MinValue;
        var toDay = DateOnly.FromDateTime(request.ToDate ?? DateTime.UtcNow);
        if (toDay < fromDay)
            (fromDay, toDay) = (toDay, fromDay);
        var periodStart = request.FromDate.HasValue ? fromDay.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue;
        var periodEndExclusive = toDay == DateOnly.MaxValue ? DateTime.MaxValue : toDay.AddDays(1).ToDateTime(TimeOnly.MinValue);

        // Profit & loss from the financial statement engine — the same lines as the income statement
        // and balance sheet, so depreciation, the book value of assets disposed of and year-end
        // adjustments are shared too. Capital and loan money is never profit.
        var ledger = await _engine.LoadAsync(request.FarmId, toDay, cancellationToken);
        var operatingRevenue = ledger.RevenueBetween(fromDay, toDay);
        var operatingExpenses = ledger.ExpensesBetween(fromDay, toDay);

        var netFarmProfit = operatingRevenue - operatingExpenses;

        // 5% goes to charity if net profit is positive; the rest is shared by the partners.
        var (charityAmount, distributableProfit) = PartnerCapital.Distributable(netFarmProfit);
        const decimal charityPercentage = PartnerCapital.CharityPercentage;

        var investors = await _investorRepository.GetByFarmIdAsync(request.FarmId, includeTransactions: true, cancellationToken: cancellationToken);
        var activeInvestors = investors.Where(i => i.IsActive).ToList();

        var totalActiveCapital = activeInvestors.Sum(i => i.CurrentCapitalBdt);

        // Partnership rule: profit is split by capital, time-weighted — each partner's capital times
        // the days it was in the farm during the period (capital-days). A partner who joined half-way
        // through gets half the weight of the same money held all period. Agreed percentages from
        // the older investor screen are kept on record but no longer used to split profit.
        var shares = PartnerCapital.TimeWeightedShares(investors, fromDay, toDay);
        var allocations = PartnerCapital.Split(Math.Round(distributableProfit, 2), shares);

        var investorShares = new List<InvestorShareDto>();
        decimal totalDistributedProfit = 0m;
        decimal totalUndistributedProfit = 0m;

        foreach (var inv in investors)
        {
            decimal capitalSharePct = totalActiveCapital > 0 && inv.IsActive
                ? Math.Round((inv.CurrentCapitalBdt / totalActiveCapital) * 100m, 2)
                : 0m;

            decimal effectiveSharePct = Math.Round(shares.GetValueOrDefault(inv.Id) * 100m, 2);

            decimal allocatedProfit = allocations.GetValueOrDefault(inv.Id);
            // Paid against THIS period's profit: distributions dated inside the period (all-time when
            // no period is given). Using the lifetime total made every period look over-distributed.
            decimal distributedProfit = inv.Transactions
                .Where(t => t.Type == InvestorTransactionType.ProfitDistribution
                            && t.TransactionDate >= periodStart
                            && (!request.ToDate.HasValue || t.TransactionDate < periodEndExclusive))
                .Sum(t => t.AmountBdt);
            if (inv.Transactions.Count == 0 && !request.FromDate.HasValue && !request.ToDate.HasValue)
                distributedProfit = inv.TotalProfitPaidBdt;
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
