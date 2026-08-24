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
);
