using System;

namespace Farm360.Contracts.Finance;

public record RecordInvestorTransactionRequest(
    string Type,
    decimal AmountBdt,
    DateTime TransactionDate,
    string? ReferenceId = null,
    string? Notes = null
);
