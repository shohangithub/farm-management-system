using System;

namespace Farm360.Contracts.Finance;

public record FinancialTransactionDto(
    Guid Id,
    Guid FarmId,
    string Type,
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string ReferenceId,
    string Notes,
    DateTime CreatedAtUtc
);
