using System;

namespace Farm360.Contracts.Finance;

public record RecordLoanRepaymentRequest(
    decimal AmountBdt,
    DateTime RepaymentDate,
    string ReferenceId = "",
    string Notes = "",
    /// <summary>Account the repayment was paid from; null = the farm's default account.</summary>
    Guid? MoneyAccountId = null,
    /// <summary>A partner paid the lender directly; books their capital contribution.</summary>
    Guid? PaidByPartnerId = null
);
