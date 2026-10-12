using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

/// <summary>
/// One account head (ledger category) on a report, e.g. "4010 Livestock Sales".
/// Group is Operating, Capital or Financing so capital money never inflates operating profit.
/// </summary>
public record AccountHeadSummaryDto(
    string Code,
    string Category,
    string Name,
    string NameBn,
    string Group,
    int Count,
    decimal TotalBdt,
    decimal PercentOfTotal
);

/// <summary>One cash-book line: a single income or expense posting.</summary>
public record CashBookLineDto(
    Guid Id,
    DateTime TransactionDate,
    string VoucherNo,
    string Type,
    string Category,
    string Code,
    string HeadName,
    string HeadNameBn,
    string Group,
    string Description,
    string Notes,
    decimal AmountBdt,
    bool IsAutomated,
    string? SourceModule
);

/// <summary>Per-day cash movement inside a date range.</summary>
public record DailyCashSummaryDto(
    DateTime Date,
    decimal OpeningBalanceBdt,
    decimal IncomeBdt,
    decimal ExpenseBdt,
    decimal NetBdt,
    decimal ClosingBalanceBdt,
    int TransactionCount
);

/// <summary>
/// Daily / date-range Income &amp; Expense (cash book) report: opening balance, head-wise summary,
/// itemised income and expense lists, day-by-day movement and closing balance.
/// </summary>
public record IncomeExpenseReportDto(
    Guid FarmId,
    DateTime FromDate,
    DateTime ToDate,
    bool IsSingleDay,
    decimal OpeningBalanceBdt,
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetBalanceBdt,
    decimal ClosingBalanceBdt,
    decimal OperatingIncomeBdt,
    decimal OperatingExpenseBdt,
    decimal NetOperatingProfitBdt,
    IReadOnlyList<AccountHeadSummaryDto> IncomeHeads,
    IReadOnlyList<AccountHeadSummaryDto> ExpenseHeads,
    IReadOnlyList<CashBookLineDto> IncomeTransactions,
    IReadOnlyList<CashBookLineDto> ExpenseTransactions,
    IReadOnlyList<DailyCashSummaryDto> DailyBreakdown
);
