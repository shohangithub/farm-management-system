using System;

namespace Farm360.Contracts.Finance;

public record RecordIncomeRequest(
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string ReferenceId = "",
    string Notes = "",
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null
);
