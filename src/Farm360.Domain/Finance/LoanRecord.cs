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

    /// <summary>Part of <see cref="TotalRepaidBdt"/> that reduced the principal (the liability).</summary>
    public decimal PrincipalRepaidBdt { get; private set; }

    /// <summary>Part of <see cref="TotalRepaidBdt"/> that was interest — a P&amp;L expense, not debt reduction.</summary>
    public decimal InterestPaidBdt { get; private set; }

    /// <summary>Flat (simple) interest over the life of the loan.</summary>
    public decimal TotalInterestBdt => Math.Round(PrincipalAmountBdt * InterestRatePercent / 100m, 2);

    public decimal TotalPayableBdt => PrincipalAmountBdt + TotalInterestBdt;

    /// <summary>Principal still owed — the figure that belongs on the balance sheet as a liability.</summary>
    public decimal OutstandingPrincipalBdt => Math.Max(0, PrincipalAmountBdt - PrincipalRepaidBdt);

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
            return Math.Max(0, TotalPayableBdt - TotalRepaidBdt);
        }
    }

    /// <summary>
    /// Progress percentage: how much of the total owed has been repaid.
    /// </summary>
    public decimal RepaymentProgressPercent
    {
        get
        {
            var totalOwed = TotalPayableBdt;
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
    /// FR-FM-13: Records a repayment against this loan and splits it into principal and interest.
    /// </summary>
    /// <remarks>
    /// Flat-rate loans are repaid in instalments that each carry principal and interest in the
    /// same proportion as the loan as a whole (principal : total interest). The final instalment
    /// absorbs rounding so principal and interest are each repaid exactly once.
    /// </remarks>
    /// <returns>The principal and interest portions of this payment.</returns>
    public (decimal Principal, decimal Interest) RecordRepayment(decimal amountBdt)
    {
        if (amountBdt <= 0)
            throw new ArgumentException("Repayment amount must be greater than zero.", nameof(amountBdt));

        var outstanding = OutstandingBalanceBdt;
        if (amountBdt > outstanding + 0.01m)
            throw new InvalidOperationException(
                $"Repayment of {amountBdt:N2} BDT exceeds the outstanding balance of {outstanding:N2} BDT.");

        var (principal, interest) = SplitRepayment(amountBdt);

        TotalRepaidBdt += amountBdt;
        PrincipalRepaidBdt += principal;
        InterestPaidBdt += interest;

        // Auto-close the loan if fully repaid
        if (TotalRepaidBdt >= TotalPayableBdt - 0.01m)
        {
            IsActive = false;
        }

        return (principal, interest);
    }

    /// <summary>How a payment of <paramref name="amountBdt"/> would split, without recording it.</summary>
    public (decimal Principal, decimal Interest) SplitRepayment(decimal amountBdt)
    {
        var payable = TotalPayableBdt;
        if (payable <= 0)
            return (amountBdt, 0m);

        var remainingPrincipal = Math.Max(0, PrincipalAmountBdt - PrincipalRepaidBdt);
        var remainingInterest = Math.Max(0, TotalInterestBdt - InterestPaidBdt);

        // Final (or over-final) instalment: settle whatever is left of each part.
        if (amountBdt >= remainingPrincipal + remainingInterest - 0.01m)
            return (remainingPrincipal, Math.Max(0, amountBdt - remainingPrincipal));

        var principal = Math.Min(remainingPrincipal, Math.Round(amountBdt * PrincipalAmountBdt / payable, 2));
        return (principal, amountBdt - principal);
    }

    /// <summary>
    /// One-off back-fill for loans recorded before principal and interest were tracked separately:
    /// applies the same proportional split to the historical total repaid.
    /// </summary>
    public void BackfillRepaymentSplit()
    {
        if (TotalRepaidBdt <= 0 || PrincipalRepaidBdt + InterestPaidBdt == TotalRepaidBdt)
            return;

        var payable = TotalPayableBdt;
        var principal = payable > 0 ? Math.Round(TotalRepaidBdt * PrincipalAmountBdt / payable, 2) : TotalRepaidBdt;
        PrincipalRepaidBdt = Math.Min(PrincipalAmountBdt, principal);
        InterestPaidBdt = TotalRepaidBdt - PrincipalRepaidBdt;
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
