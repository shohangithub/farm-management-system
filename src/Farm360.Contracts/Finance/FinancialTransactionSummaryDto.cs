using System;

namespace Farm360.Contracts.Finance;

public record FinancialTransactionSummaryDto(
    decimal TotalIncomeBdt,
    decimal TotalExpenseBdt,
    decimal NetBalanceBdt
);
