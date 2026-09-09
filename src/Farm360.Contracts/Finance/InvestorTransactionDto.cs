using System;

namespace Farm360.Contracts.Finance;

public record InvestorTransactionDto(
    Guid Id,
    Guid InvestorId,
    Guid FarmId,
    string Type,
    decimal AmountBdt,
    DateTime TransactionDate,
    string? ReferenceId,
    string? Notes,
    Guid? FinancialTransactionId,
    DateTime CreatedAtUtc
);
