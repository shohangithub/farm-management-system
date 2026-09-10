using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

public record TrialBalanceLineDto(
    string AccountCode,
    string AccountName,
    string CategoryGroup, // "Asset", "Liability", "Equity", "Revenue", "Expense"
    decimal DebitBdt,
    decimal CreditBdt
);

public record TrialBalanceDto(
    Guid FarmId,
    DateTime AsOfDate,
    IReadOnlyList<TrialBalanceLineDto> Lines,
    decimal TotalDebitBdt,
    decimal TotalCreditBdt,
    bool IsBalanced,
    decimal DifferenceBdt
);
