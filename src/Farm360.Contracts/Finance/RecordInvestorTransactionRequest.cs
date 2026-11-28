using System;

namespace Farm360.Contracts.Finance;

public record RecordInvestorTransactionRequest(
    string Type,
    decimal AmountBdt,
    DateTime TransactionDate,
    string? ReferenceId = null,
    string? Notes = null,
    /// <summary>
    /// The money account the contribution goes INTO, or the withdrawal / profit payout comes OUT of.
    /// Null = the farm's default account.
    /// </summary>
    Guid? MoneyAccountId = null
);
