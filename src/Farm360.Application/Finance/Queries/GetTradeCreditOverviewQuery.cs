using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <summary>
/// The credit book: who owes the farm, whom the farm owes, how overdue it is, and proof that the
/// bills add up to the Accounts Receivable / Payable balances in the ledger.
/// </summary>
public sealed record GetTradeCreditOverviewQuery(Guid FarmId, bool IncludeSettled = false) : IRequest<TradeCreditOverviewDto>;

public sealed class GetTradeCreditOverviewQueryHandler(
    ITradeBillRepository bills,
    IFinancialTransactionRepository transactions) : IRequestHandler<GetTradeCreditOverviewQuery, TradeCreditOverviewDto>
{
    public async Task<TradeCreditOverviewDto> Handle(GetTradeCreditOverviewQuery request, CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var all = await bills.GetByFarmIdAsync(request.FarmId, cancellationToken: cancellationToken);
        var payments = (await bills.GetPaymentsByFarmIdAsync(request.FarmId, cancellationToken)).ToLookup(p => p.BillId);

        var postings = await transactions.GetAllByFarmIdAsync(request.FarmId, cancellationToken);
        var balances = AccountChart.Balances(postings);

        var payables = Side(TradeBillKind.Payable, all, asOf, -balances.GetValueOrDefault(AccountChart.PayableAccount));
        var receivables = Side(TradeBillKind.Receivable, all, asOf, balances.GetValueOrDefault(AccountChart.ReceivableAccount));

        var listed = all
            .Where(b => request.IncludeSettled || b.IsOpen || b.Status == TradeBillStatus.Overpaid)
            .OrderBy(b => b.IsOpen ? 0 : 1)
            .ThenBy(b => b.DueDate ?? b.BillDate)
            .Select(b => b.ToDto(payments[b.Id], asOf))
            .ToList();

        return new TradeCreditOverviewDto(request.FarmId, asOf, payables, receivables, listed);
    }

    private static TradeCreditSideDto Side(TradeBillKind kind, IReadOnlyList<TradeBill> all, DateOnly asOf, decimal ledgerBalance)
    {
        var bills = all.Where(b => b.Kind == kind).ToList();
        var open = bills.Where(b => b.IsOpen).ToList();
        var outstanding = bills.Sum(b => b.OutstandingBdt);
        var weekEnd = asOf.AddDays(7);

        var aging = TradeBillMapping.AgingBuckets
            .Select(bucket =>
            {
                var inBucket = open.Where(b => TradeBillMapping.Bucket(TradeBillMapping.DaysOverdue(b, asOf)) == bucket.Key).ToList();
                return new AgingBucketDto(bucket.Key, bucket.Label, inBucket.Count, R(inBucket.Sum(b => b.OutstandingBdt)));
            })
            .ToList();

        var parties = bills
            .Where(b => b.IsOpen || b.Status == TradeBillStatus.Overpaid)
            .GroupBy(b => b.PartyName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PartyBalanceDto(
                g.First().PartyName,
                g.Select(b => b.PartyPhone).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)),
                g.Count(b => b.IsOpen),
                R(g.Sum(b => b.AmountBdt)),
                R(g.Sum(b => b.PaidBdt + b.AdjustedBdt)),
                R(g.Sum(b => b.OutstandingBdt)),
                R(g.Where(b => TradeBillMapping.DaysOverdue(b, asOf) > 0).Sum(b => b.OutstandingBdt)),
                g.Where(b => b.IsOpen).Select(b => b.DueDate).Where(d => d.HasValue).Min()))
            .OrderByDescending(p => p.OverdueBdt)
            .ThenByDescending(p => p.OutstandingBdt)
            .ToList();

        return new TradeCreditSideDto(
            kind.ToString(),
            R(outstanding),
            R(open.Where(b => TradeBillMapping.DaysOverdue(b, asOf) > 0).Sum(b => b.OutstandingBdt)),
            R(open.Where(b => b.DueDate is { } d && d >= asOf && d <= weekEnd).Sum(b => b.OutstandingBdt)),
            open.Count,
            R(ledgerBalance),
            Math.Abs(ledgerBalance - outstanding) <= AccountingReconciliationService.Tolerance,
            aging,
            parties);
    }

    private static decimal R(decimal value) => Math.Round(value, 2);
}

public sealed record GetTradeBillQuery(Guid FarmId, Guid BillId) : IRequest<TradeBillDto>;

public sealed class GetTradeBillQueryHandler(ITradeBillRepository bills) : IRequestHandler<GetTradeBillQuery, TradeBillDto>
{
    public async Task<TradeBillDto> Handle(GetTradeBillQuery request, CancellationToken cancellationToken)
    {
        var bill = await bills.GetByIdAsync(request.BillId, cancellationToken);
        if (bill is null || bill.FarmId != request.FarmId)
            throw new NotFoundException(nameof(TradeBill), request.BillId);

        return bill.ToDto(await bills.GetPaymentsAsync(bill.Id, cancellationToken), DateOnly.FromDateTime(DateTime.Today));
    }
}
