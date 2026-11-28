using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Application.Finance.Services;

/// <summary>
/// The partnership rules, in one place:
/// <list type="bullet">
/// <item>All partners fund the whole farm from one pooled fund.</item>
/// <item>A partner's <b>capital</b> = money put in − capital taken out. Profit paid out does not
/// reduce capital.</item>
/// <item>A partner's <b>share</b> today = their capital ÷ all partners' capital.</item>
/// <item>The <b>profit of a period</b> is split by <b>capital-days</b>: each partner's capital
/// multiplied by the number of days it was in the farm during the period. Someone who put in
/// ৳1,00,000 for the whole year gets twice the share of someone who put in the same amount
/// half-way through.</item>
/// </list>
/// Agreed profit percentages and share counts from the older share-market module are not used to
/// split profit; capital is the rule.
/// </summary>
public static class PartnerCapital
{
    /// <summary>
    /// Part of a positive net profit set aside for charity before partners share it (kept from the
    /// investor profit-sharing screen this replaces).
    /// </summary>
    public const decimal CharityPercentage = 5.0m;

    public static bool IsCapitalMovement(InvestorTransactionType type) =>
        type is InvestorTransactionType.CapitalContribution or InvestorTransactionType.CapitalWithdrawal;

    /// <summary>+amount for money put in, −amount for capital taken out, 0 for profit paid out.</summary>
    public static decimal CapitalEffect(InvestorTransaction t)
    {
        ArgumentNullException.ThrowIfNull(t);
        return t.Type switch
        {
            InvestorTransactionType.CapitalContribution => t.AmountBdt,
            InvestorTransactionType.CapitalWithdrawal => -t.AmountBdt,
            _ => 0m,
        };
    }

    /// <summary>Capital at the end of <paramref name="day"/> (movements on that day count).</summary>
    public static decimal CapitalAt(IEnumerable<InvestorTransaction> transactions, DateOnly day) =>
        transactions.Where(t => DateOnly.FromDateTime(t.TransactionDate) <= day).Sum(CapitalEffect);

    /// <summary>
    /// Σ over each day of [from, to] of the capital held at the end of that day. Money put in on a day
    /// counts for that day.
    /// </summary>
    public static decimal CapitalDays(IEnumerable<InvestorTransaction> transactions, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(transactions);
        if (to < from)
            return 0m;

        var moves = transactions
            .Where(t => IsCapitalMovement(t.Type))
            .Select(t => (Day: DateOnly.FromDateTime(t.TransactionDate), Amount: CapitalEffect(t)))
            .ToList();

        var capital = moves.Where(m => m.Day < from).Sum(m => m.Amount);
        var total = 0m;
        var cursor = from;
        foreach (var group in moves.Where(m => m.Day >= from && m.Day <= to).GroupBy(m => m.Day).OrderBy(g => g.Key))
        {
            total += Math.Max(0m, capital) * (group.Key.DayNumber - cursor.DayNumber);
            capital += group.Sum(m => m.Amount);
            cursor = group.Key;
        }

        total += Math.Max(0m, capital) * (to.DayNumber - cursor.DayNumber + 1);
        return total;
    }

    /// <summary>Each partner's share (0–1) of the period's profit, by capital-days. Empty when nobody had capital in.</summary>
    public static Dictionary<Guid, decimal> TimeWeightedShares(IEnumerable<Investor> partners, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(partners);
        var days = partners.ToDictionary(p => p.Id, p => CapitalDays(p.Transactions, from, to));
        return Normalise(days);
    }

    /// <summary>Each partner's share (0–1) of the pooled fund at the end of <paramref name="day"/>.</summary>
    public static Dictionary<Guid, decimal> SharesAt(IEnumerable<Investor> partners, DateOnly day)
    {
        ArgumentNullException.ThrowIfNull(partners);
        var capital = partners.ToDictionary(p => p.Id, p => Math.Max(0m, CapitalAt(p.Transactions, day)));
        return Normalise(capital);
    }

    /// <summary>The part of a period's net profit the partners share (after the charity set-aside).</summary>
    public static (decimal Charity, decimal Distributable) Distributable(decimal netProfitBdt)
    {
        var charity = netProfitBdt > 0 ? Math.Round(netProfitBdt * CharityPercentage / 100m, 2) : 0m;
        return (charity, netProfitBdt - charity);
    }

    /// <summary>
    /// Splits <paramref name="amountBdt"/> by <paramref name="shares"/>, rounding to the paisa and
    /// putting any rounding remainder on the largest share so the parts add up exactly.
    /// </summary>
    public static Dictionary<Guid, decimal> Split(decimal amountBdt, IReadOnlyDictionary<Guid, decimal> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        var parts = shares.ToDictionary(s => s.Key, s => Math.Round(amountBdt * s.Value, 2));
        if (parts.Count > 0)
        {
            var remainder = amountBdt - parts.Values.Sum();
            if (remainder != 0m && shares.Values.Sum() > 0m)
            {
                var largest = shares.OrderByDescending(s => s.Value).First().Key;
                parts[largest] += remainder;
            }
        }

        return parts;
    }

    /// <summary>
    /// The fiscal years (clipped to [<paramref name="from"/>, <paramref name="to"/>]) a lifetime of
    /// profit is allocated over — each one split by its own capital-days.
    /// </summary>
    public static IEnumerable<(DateOnly From, DateOnly To)> FiscalPeriods(DateOnly from, DateOnly to, int fiscalYearStartMonth)
    {
        if (to < from)
            yield break;

        var startMonth = fiscalYearStartMonth is >= 1 and <= 12 ? fiscalYearStartMonth : 1;
        var year = from.Month >= startMonth ? from.Year : from.Year - 1;
        var fyStart = new DateOnly(year, startMonth, 1);
        while (fyStart <= to)
        {
            var fyEnd = fyStart.AddYears(1).AddDays(-1);
            var start = fyStart < from ? from : fyStart;
            var end = fyEnd > to ? to : fyEnd;
            if (start <= end)
                yield return (start, end);
            fyStart = fyStart.AddYears(1);
        }
    }

    private static Dictionary<Guid, decimal> Normalise(Dictionary<Guid, decimal> weights)
    {
        var total = weights.Values.Where(v => v > 0).Sum();
        return total <= 0
            ? new Dictionary<Guid, decimal>()
            : weights.Where(w => w.Value > 0).ToDictionary(w => w.Key, w => w.Value / total);
    }
}
