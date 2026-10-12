using System;

namespace Farm360.Contracts.Finance;

public record LoanRecordDto(
    Guid Id,
    Guid FarmId,
    string LenderName,
    decimal PrincipalAmountBdt,
    decimal InterestRatePercent,
    DateTime DisbursementDate,
    string Schedule,
    decimal TotalRepaidBdt,
    decimal OutstandingBalanceBdt,
    decimal RepaymentProgressPercent,
    string? Notes,
    bool IsActive,
    DateTime CreatedAtUtc
)
{
    /// <summary>Flat interest over the life of the loan.</summary>
    public decimal TotalInterestBdt { get; init; }

    /// <summary>Principal + interest.</summary>
    public decimal TotalPayableBdt { get; init; }

    /// <summary>Part of the repayments that reduced the debt.</summary>
    public decimal PrincipalRepaidBdt { get; init; }

    /// <summary>Part of the repayments that was interest (an expense).</summary>
    public decimal InterestPaidBdt { get; init; }

    /// <summary>Principal still owed — the balance-sheet liability.</summary>
    public decimal OutstandingPrincipalBdt { get; init; }
}
