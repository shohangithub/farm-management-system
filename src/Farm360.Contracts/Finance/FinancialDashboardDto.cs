using System;

namespace Farm360.Contracts.Finance;

public record FinancialDashboardDto(
    Guid FarmId,
    decimal RevenueMtdBdt,
    decimal ExpensesMtdBdt,
    decimal NetProfitMtdBdt,
    decimal RevenueMomPercent,
    decimal ExpensesMomPercent,
    decimal NetProfitMomPercent
);
