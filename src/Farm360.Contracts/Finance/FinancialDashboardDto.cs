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
);
