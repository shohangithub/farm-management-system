using System;
using System.Collections.Generic;

namespace Farm360.Contracts.Finance;

/// <summary>
/// One balance-sheet account checked against the register that should back it — the books'
/// Inventory against the stock on hand, Loans Payable against the loan register, and so on.
/// A difference means something was recorded on one side and not the other.
/// </summary>
public record ReconciliationLineDto(
    string Key,
    string AccountCode,
    string Account,
    string AccountBn,
    decimal BooksBdt,
    decimal RegisterBdt,
    decimal DifferenceBdt,
    bool IsReconciled,
    string RegisterSource,
    string Explanation
);

/// <summary>A specific accounting problem, with whether the repair tool can fix it automatically.</summary>
public record AccountingIssueDto(
    string Key,
    string Severity,
    string Title,
    string Detail,
    int Count,
    decimal AmountBdt,
    bool CanAutoFix
)
{
    /// <summary>The records the issue is about, when it names them (e.g. each money account below zero).</summary>
    public IReadOnlyList<AccountingIssueItemDto>? Items { get; init; }
}

/// <summary>One named record of an issue and its amount.</summary>
public record AccountingIssueItemDto(Guid? Id, string Name, decimal AmountBdt);

public record AccountingHealthDto(
    Guid FarmId,
    DateTime CheckedAtUtc,
    bool IsHealthy,
    decimal CashBalanceBdt,
    IReadOnlyList<ReconciliationLineDto> Reconciliations,
    IReadOnlyList<AccountingIssueDto> Issues
);

public record RepairLedgerRequest(bool AdjustInventoryToStockCount = false);

public record LedgerRepairResultDto(
    int LoanDisbursementsPosted,
    int LoanRepaymentsSplit,
    int AnimalsResynced,
    int DisposalsPosted,
    decimal InventoryAdjustmentBdt,
    IReadOnlyList<string> Messages,
    AccountingHealthDto HealthAfter
);
