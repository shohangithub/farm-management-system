using System;

namespace Farm360.Contracts.Finance;

public record CreateLoanRecordRequest(
    string LenderName,
    decimal PrincipalAmountBdt,
    decimal InterestRatePercent,
    DateTime DisbursementDate,
    string Schedule,
    string? Notes = null,
    /// <summary>Account the loan money was received into; null = the farm's default account.</summary>
    Guid? MoneyAccountId = null
);
