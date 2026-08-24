using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// LoanRecord — Aggregate root for tracking loans and investments.
/// PRD FR-FM-12: Loan/investment recording with lender, amount, interest, schedule.
/// PRD FR-FM-13: Track repayments and display outstanding balance.
/// </summary>
public sealed class LoanRecord : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string LenderName { get; private set; } = string.Empty;
    public decimal PrincipalAmountBdt { get; private set; }
    public decimal InterestRatePercent { get; private set; }
    public DateTime DisbursementDate { get; private set; }
    public RepaymentSchedule Schedule { get; private set; }
    public decimal TotalRepaidBdt { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Computed outstanding balance: Principal + Accrued Interest - Total Repaid.
    /// Simplified for MVP: uses simple interest on principal.
    /// </summary>
    public decimal OutstandingBalanceBdt
    {
        get
        {
            var totalOwed = PrincipalAmountBdt * (1 + InterestRatePercent / 100m);
            return Math.Max(0, totalOwed - TotalRepaidBdt);
        }
    }

    /// <summary>
    /// Progress percentage: how much of the total owed has been repaid.
    /// </summary>
    public decimal RepaymentProgressPercent
    {
        get
        {
            var totalOwed = PrincipalAmountBdt * (1 + InterestRatePercent / 100m);
            if (totalOwed <= 0) return 100m;
            return Math.Min(100m, Math.Round(TotalRepaidBdt / totalOwed * 100m, 1));
        }
    }

    private LoanRecord() { } // For EF Core

    public static LoanRecord Create(
        Guid tenantId,
        Guid farmId,
        string lenderName,
        decimal principalAmountBdt,
        decimal interestRatePercent,
        DateTime disbursementDate,
        RepaymentSchedule schedule,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(lenderName))
            throw new ArgumentException("Lender name is required.", nameof(lenderName));
        if (principalAmountBdt <= 0)
            throw new ArgumentException("Principal amount must be greater than zero.", nameof(principalAmountBdt));
        if (interestRatePercent < 0)
            throw new ArgumentException("Interest rate cannot be negative.", nameof(interestRatePercent));

        var loan = new LoanRecord
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            LenderName = lenderName.Trim(),
            PrincipalAmountBdt = principalAmountBdt,
            InterestRatePercent = interestRatePercent,
            DisbursementDate = disbursementDate,
            Schedule = schedule,
            TotalRepaidBdt = 0,
            Notes = notes?.Trim()
        };

        loan.SetTenantId(tenantId);
        return loan;
    }

    /// <summary>
    /// FR-FM-13: Records a repayment against this loan.
    /// </summary>
    public void RecordRepayment(decimal amountBdt)
    {
        if (amountBdt <= 0)
            throw new ArgumentException("Repayment amount must be greater than zero.", nameof(amountBdt));

        TotalRepaidBdt += amountBdt;

        // Auto-close the loan if fully repaid
        var totalOwed = PrincipalAmountBdt * (1 + InterestRatePercent / 100m);
        if (TotalRepaidBdt >= totalOwed)
        {
            IsActive = false;
        }
    }

    public void UpdateDetails(
        string lenderName,
        decimal interestRatePercent,
        RepaymentSchedule schedule,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(lenderName))
            throw new ArgumentException("Lender name is required.", nameof(lenderName));

        LenderName = lenderName.Trim();
        InterestRatePercent = interestRatePercent;
        Schedule = schedule;
        Notes = notes?.Trim();
    }
}
