using System;

namespace Farm360.Contracts.Finance;

public record CreateFinancialTransactionRequest(
    string Type,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string ReferenceId,
    string Notes
);
