using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record MonthlyPnLReportDto(
    Guid FarmId,
    int Year,
    int Month,
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetProfitBdt,
    IReadOnlyDictionary<string, decimal> IncomeByCategory,
    IReadOnlyDictionary<string, decimal> ExpenseByCategory
);
