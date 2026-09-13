using System;

namespace Farm360.Contracts.Finance;

public record ShareTransactionDto(
    Guid Id,
    Guid FarmId,
    Guid InvestorId,
    string InvestorName,
    string Type,
    int ShareCount,
    decimal PricePerShareBdt,
    decimal TotalAmountBdt,
    DateTime TransactionDate,
    Guid? CounterpartyInvestorId,
    string? CounterpartyInvestorName,
    string? ReferenceId,
    string? Notes,
    Guid? FinancialTransactionId,
    DateTime CreatedAtUtc
);
