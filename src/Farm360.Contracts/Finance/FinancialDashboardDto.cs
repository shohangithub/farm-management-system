using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record MonthlyCashFlowPointDto(string MonthLabel, decimal IncomeBdt, decimal ExpenseBdt, decimal NetProfitBdt);
public record CategoryExpenseBreakdownDto(string Category, decimal AmountBdt, decimal Percentage);

public record FinancialDashboardDto(
    Guid FarmId,
    decimal RevenueMtdBdt,
    decimal ExpensesMtdBdt,
    decimal NetProfitMtdBdt,
    decimal RevenueMomPercent,
    decimal ExpensesMomPercent,
    decimal NetProfitMomPercent,
    decimal RevenueYtdBdt = 0m,
    decimal ExpensesYtdBdt = 0m,
    decimal NetProfitYtdBdt = 0m,
    decimal ProfitMarginPercent = 0m,
    IReadOnlyList<MonthlyCashFlowPointDto>? CashFlowTrend = null,
    IReadOnlyList<CategoryExpenseBreakdownDto>? ExpenseBreakdown = null,
    IReadOnlyList<FinancialTransactionDto>? RecentTransactions = null
)
{
    /// <summary>First day of the fiscal-year-to-date window the YTD figures cover.</summary>
    public DateTime? FiscalYearStart { get; init; }

    public int FiscalYearStartMonth { get; init; }

    /// <summary>Cash &amp; bank today, split by money account (cash in hand, bank, wallet).</summary>
    public IReadOnlyList<MoneyAccountBreakdownDto>? MoneyAccounts { get; init; }
}
