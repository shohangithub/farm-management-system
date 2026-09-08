using System;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Contracts.Finance;

public record UpdateFinancialTransactionRequest(
    TransactionCategory Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string Notes,
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null
);
