using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using MediatR;

namespace Farm360.Application.Finance.Queries;

/// <summary>
/// Daily or date-range Income &amp; Expense (cash book) report. Pass the same date twice for a
/// single-day report.
/// </summary>
public record GetIncomeExpenseReportQuery(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate
) : IRequest<IncomeExpenseReportDto>;

public class GetIncomeExpenseReportQueryHandler : IRequestHandler<GetIncomeExpenseReportQuery, IncomeExpenseReportDto>
{
    private const int MaxDaysListedInFull = 92;

    private readonly IFinancialStatementEngine _engine;

    public GetIncomeExpenseReportQueryHandler(IFinancialStatementEngine engine)
    {
        _engine = engine;
    }

    public async Task<IncomeExpenseReportDto> Handle(GetIncomeExpenseReportQuery request, CancellationToken cancellationToken)
    {
        var (start, endExclusive) = AccountChart.Window(request.FromDate, request.ToDate);
        var fromDay = DateOnly.FromDateTime(start);
        var toDay = DateOnly.FromDateTime(endExclusive.AddDays(-1));

        var ledger = await _engine.LoadAsync(request.FarmId, toDay, cancellationToken);

        // Opening balance is the balance sheet's cash at the close of the day before the period —
        // opening entries included, postings on or before the books start replaced by them — so
        // the cash book and the balance sheet always agree.
        var openingBalance = ledger.OpeningBalancesFor(fromDay).GetValueOrDefault(AccountChart.CashAccount);

        // A cash book lists money that actually moved. Non-cash postings (feed issued from stock,
        // the cost of an animal sold) are real costs but belong to the P&L, not the cash book —
        // the cash for them left when the stock or animal was bought.
        var inPeriod = ledger.PostingsBetween(fromDay, toDay).Where(t => t.AffectsCash).ToList();

        var income = inPeriod.Where(t => t.Type == TransactionType.Income).ToList();
        var expense = inPeriod.Where(t => t.Type == TransactionType.Expense).ToList();

        var totalIncome = income.Sum(t => t.AmountBdt);
        var totalExpense = expense.Sum(t => t.AmountBdt);
        var operatingIncome = income.Where(AccountChart.IsOperating).Sum(t => t.AmountBdt);
        var operatingExpense = expense.Where(AccountChart.IsOperating).Sum(t => t.AmountBdt);

        var daily = BuildDailyBreakdown(inPeriod, start, endExclusive, openingBalance);

        return new IncomeExpenseReportDto(
            request.FarmId,
            start,
            endExclusive.AddDays(-1),
            endExclusive.AddDays(-1) == start,
            Math.Round(openingBalance, 2),
            Math.Round(totalIncome, 2),
            Math.Round(totalExpense, 2),
            Math.Round(totalIncome - totalExpense, 2),
            Math.Round(openingBalance + totalIncome - totalExpense, 2),
            Math.Round(operatingIncome, 2),
            Math.Round(operatingExpense, 2),
            Math.Round(operatingIncome - operatingExpense, 2),
            SummariseHeads(income, totalIncome),
            SummariseHeads(expense, totalExpense),
            income.Select(ToLine).ToList(),
            expense.Select(ToLine).ToList(),
            daily)
        {
            // The accrual result for the same days, from the income statement's lines (stock used,
            // animals sold at cost, depreciation, disposals, adjustments) — not a cash figure.
            NetProfitBdt = Math.Round(ledger.ProfitBetween(fromDay, toDay), 2),
        };
    }

    /// <summary>
    /// Cash-book head summary: what was actually received or paid under each head (gross amounts).
    /// For profit &amp; loss use <see cref="SummariseProfitAndLossHeads"/>.
    /// </summary>
    internal static IReadOnlyList<AccountHeadSummaryDto> SummariseHeads(
        IEnumerable<FinancialTransaction> transactions,
        decimal total) =>
        transactions
            .GroupBy(t => t.Category)
            .Select(g =>
            {
                var head = AccountChart.For(g.Key);
                var sum = g.Sum(t => t.AmountBdt);
                return new AccountHeadSummaryDto(
                    head.Code,
                    g.Key.ToString(),
                    head.Name,
                    head.NameBn,
                    head.Group,
                    g.Count(),
                    Math.Round(sum, 2),
                    total > 0 ? Math.Round(sum / total * 100m, 2) : 0m);
            })
            .OrderByDescending(h => h.TotalBdt)
            .ToList();

    /// <summary>
    /// Profit &amp; loss head summary for [from, to] from the ledger: revenue heads by their credit
    /// movement, expense heads by their debit movement — net of refunds and disposal proceeds, and
    /// including depreciation, the book value of assets disposed of and year-end adjustments — so
    /// the heads add up to the period's total revenue or expenses.
    /// </summary>
    internal static IReadOnlyList<AccountHeadSummaryDto> SummariseProfitAndLossHeads(
        FarmLedger ledger,
        DateOnly from,
        DateOnly to,
        bool revenue)
    {
        var heads = ledger.ProfitAndLossLinesBetween(from, to)
            .Where(l => FarmLedger.IsRevenue(l.Account) == revenue)
            .GroupBy(l => l.Account)
            .Select(g => (Code: g.Key, Count: g.Count(), Amount: revenue ? -g.Sum(l => l.Amount) : g.Sum(l => l.Amount)))
            .Where(h => h.Amount != 0m)
            .ToList();
        // Percentages are of the heads that add to the total; a gain (a negative expense head)
        // reduces the total but must not push the other heads above 100%.
        var total = heads.Where(h => h.Amount > 0m).Sum(h => h.Amount);

        return heads
            .Select(h =>
            {
                var account = AccountChart.Account(h.Code);
                return new AccountHeadSummaryDto(
                    h.Code,
                    AccountChart.CategoryKey(h.Code),
                    account.Name,
                    account.NameBn,
                    AccountChart.Operating,
                    h.Count,
                    Math.Round(h.Amount, 2),
                    total > 0 ? Math.Round(h.Amount / total * 100m, 2) : 0m);
            })
            .OrderByDescending(h => h.TotalBdt)
            .ToList();
    }

    internal static CashBookLineDto ToLine(FinancialTransaction t)
    {
        var head = AccountChart.For(t.Category);
        return new CashBookLineDto(
            t.Id,
            t.TransactionDate,
            AccountChart.VoucherNo(t),
            t.Type.ToString(),
            t.Category.ToString(),
            head.Code,
            head.Name,
            head.NameBn,
            head.Group,
            AccountChart.Describe(t),
            t.Notes,
            Math.Round(t.AmountBdt, 2),
            t.IsAutomated,
            t.SourceModule);
    }

    private static List<DailyCashSummaryDto> BuildDailyBreakdown(
        IReadOnlyCollection<FinancialTransaction> inPeriod,
        DateTime start,
        DateTime endExclusive,
        decimal openingBalance)
    {
        var byDay = inPeriod.ToLookup(t => t.TransactionDate.Date);
        var result = new List<DailyCashSummaryDto>();
        var running = openingBalance;

        // Up to a quarter, every calendar day is listed so quiet days are visible. Beyond that a
        // row per empty day is noise (an "all time" run would be thousands of zero rows), so only
        // days that actually moved cash are kept — the running balance is unaffected either way.
        var listEveryDay = (endExclusive - start).TotalDays <= MaxDaysListedInFull;

        for (var day = start; day < endExclusive; day = day.AddDays(1))
        {
            var txs = byDay[day].ToList();
            if (txs.Count == 0 && !listEveryDay)
            {
                continue;
            }

            var dayIncome = txs.Where(t => t.Type == TransactionType.Income).Sum(t => t.AmountBdt);
            var dayExpense = txs.Where(t => t.Type == TransactionType.Expense).Sum(t => t.AmountBdt);
            var opening = running;
            running += dayIncome - dayExpense;

            result.Add(new DailyCashSummaryDto(
                day,
                Math.Round(opening, 2),
                Math.Round(dayIncome, 2),
                Math.Round(dayExpense, 2),
                Math.Round(dayIncome - dayExpense, 2),
                Math.Round(running, 2),
                txs.Count));
        }

        return result;
    }
}
