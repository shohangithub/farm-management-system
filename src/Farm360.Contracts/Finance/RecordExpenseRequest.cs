using System;

namespace Farm360.Contracts.Finance;

public record RecordExpenseRequest(
    string Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    string Description,
    string ReferenceId = "",
    string Notes = "",
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    CreditTermsRequest? Credit = null,
    /// <summary>Account the money was paid from; null = the farm's default account.</summary>
    Guid? MoneyAccountId = null,
    /// <summary>A partner paid this directly; books their capital contribution of the same amount.</summary>
    Guid? PaidByPartnerId = null
);
