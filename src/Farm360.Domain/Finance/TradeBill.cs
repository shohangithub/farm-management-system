using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// An unpaid (or part-paid) credit deal: a supplier bill the farm owes, or a customer invoice owed
/// to the farm. It is the register behind Accounts Payable / Accounts Receivable.
/// </summary>
/// <remarks>
/// <para>
/// A bill always mirrors one ledger posting — the purchase or sale it was raised for — identified,
/// like every automated posting, by <see cref="SourceDocumentId"/> + <see cref="Category"/>. While
/// the bill is live that posting is settled against Payable/Receivable instead of Cash, so the cost
/// or revenue is recognised on the deal date while the cash book only moves when money is paid.
/// </para>
/// <para>
/// <see cref="AmountBdt"/> follows the source posting (a corrected price restates it). The balance
/// is reduced by payments (<see cref="PaidBdt"/>) and by adjustments (<see cref="AdjustedBdt"/>):
/// goods returned against the bill, a supplier waiving the rest, or a customer debt written off.
/// </para>
/// </remarks>
public sealed class TradeBill : AuditableEntity, IAggregateRoot
{
    /// <summary>Amounts within this of each other are treated as equal (rounding).</summary>
    public const decimal Tolerance = 0.01m;

    public Guid FarmId { get; private set; }
    public TradeBillKind Kind { get; private set; }

    public string PartyName { get; private set; } = string.Empty;
    public string? PartyPhone { get; private set; }

    /// <summary>The supplier, when the bill came from a purchase order.</summary>
    public Guid? SupplierId { get; private set; }

    public Guid SourceDocumentId { get; private set; }
    public TransactionCategory Category { get; private set; }
    public string? SourceModule { get; private set; }

    public string ReferenceNo { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    public DateOnly BillDate { get; private set; }
    public DateOnly? DueDate { get; private set; }

    public decimal AmountBdt { get; private set; }
    public decimal PaidBdt { get; private set; }
    public decimal AdjustedBdt { get; private set; }
    public bool IsCancelled { get; private set; }

    /// <summary>Still to be paid. Negative when more was settled than the bill now comes to.</summary>
    public decimal OutstandingBdt => AmountBdt - PaidBdt - AdjustedBdt;

    public bool HasSettlements => PaidBdt > 0 || AdjustedBdt > 0;

    public TradeBillStatus Status =>
        IsCancelled ? TradeBillStatus.Cancelled
        : OutstandingBdt < -Tolerance ? TradeBillStatus.Overpaid
        : OutstandingBdt <= Tolerance ? TradeBillStatus.Paid
        : HasSettlements ? TradeBillStatus.PartiallyPaid
        : TradeBillStatus.Open;

    public bool IsOpen => !IsCancelled && OutstandingBdt > Tolerance;

    /// <summary>The settlement account the source posting is booked against while this bill is live.</summary>
    public SettlementAccount Settlement =>
        Kind == TradeBillKind.Payable ? SettlementAccount.Payable : SettlementAccount.Receivable;

    private TradeBill() { } // EF Core

    public static TradeBill Create(
        Guid tenantId,
        Guid farmId,
        TradeBillKind kind,
        string partyName,
        Guid sourceDocumentId,
        TransactionCategory category,
        decimal amountBdt,
        DateOnly billDate,
        DateOnly? dueDate,
        string referenceNo,
        string description,
        string? sourceModule = null,
        Guid? supplierId = null,
        string? partyPhone = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(partyName))
            throw new ArgumentException("Supplier / customer name is required for a credit bill.", nameof(partyName));
        if (amountBdt <= 0)
            throw new ArgumentException("A credit bill must be for more than zero.", nameof(amountBdt));
        if (sourceDocumentId == Guid.Empty)
            throw new ArgumentException("A credit bill must point at the purchase or sale it was raised for.", nameof(sourceDocumentId));
        if (dueDate is { } due && due < billDate)
            throw new ArgumentException("The due date cannot be before the bill date.", nameof(dueDate));

        var bill = new TradeBill
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Kind = kind,
            PartyName = partyName.Trim(),
            PartyPhone = partyPhone?.Trim(),
            SupplierId = supplierId,
            SourceDocumentId = sourceDocumentId,
            Category = category,
            SourceModule = sourceModule,
            ReferenceNo = referenceNo?.Trim() ?? string.Empty,
            Description = description?.Trim() ?? string.Empty,
            Notes = notes?.Trim(),
            BillDate = billDate,
            DueDate = dueDate,
            AmountBdt = Math.Round(amountBdt, 2),
        };

        bill.SetTenantId(tenantId);
        return bill;
    }

    /// <summary>Changes who the bill is with and when it is due.</summary>
    public void UpdateTerms(string partyName, string? partyPhone, DateOnly? dueDate, string? notes)
    {
        if (string.IsNullOrWhiteSpace(partyName))
            throw new ArgumentException("Supplier / customer name is required for a credit bill.", nameof(partyName));
        if (dueDate is { } due && due < BillDate)
            throw new ArgumentException("The due date cannot be before the bill date.", nameof(dueDate));

        PartyName = partyName.Trim();
        PartyPhone = partyPhone?.Trim();
        DueDate = dueDate;
        Notes = notes?.Trim();
    }

    /// <summary>
    /// Follows a change to the source posting (price corrected, quantity changed). A reduction below
    /// what was already settled leaves the bill <see cref="TradeBillStatus.Overpaid"/> — money to
    /// recover — rather than silently losing a payment.
    /// </summary>
    public void Revise(decimal amountBdt, DateOnly billDate)
    {
        if (amountBdt < 0)
            throw new ArgumentException("Bill amount cannot be negative.", nameof(amountBdt));

        AmountBdt = Math.Round(amountBdt, 2);
        BillDate = billDate;
        if (DueDate is { } due && due < billDate)
            DueDate = billDate;
    }

    /// <summary>Records money paid (payable) or collected (receivable) against the bill.</summary>
    public void ApplyPayment(decimal amountBdt)
    {
        EnsureLive();
        if (amountBdt <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amountBdt));
        if (amountBdt > OutstandingBdt + Tolerance)
        {
            throw new InvalidOperationException(
                $"Payment of {amountBdt:N2} BDT is more than the {Math.Max(0, OutstandingBdt):N2} BDT outstanding on {ReferenceNo}.");
        }

        PaidBdt = Math.Round(PaidBdt + amountBdt, 2);
    }

    /// <summary>Undoes a payment that was voided.</summary>
    public void ReversePayment(decimal amountBdt)
    {
        if (amountBdt <= 0 || amountBdt > PaidBdt + Tolerance)
            throw new InvalidOperationException("Cannot reverse more than has been paid on this bill.");

        PaidBdt = Math.Max(0, Math.Round(PaidBdt - amountBdt, 2));
    }

    /// <summary>
    /// Reduces the balance without cash: goods returned against the bill, the supplier waiving the
    /// remainder, or a customer debt written off as bad. A negative amount undoes an earlier adjustment.
    /// </summary>
    public void Adjust(decimal amountBdt)
    {
        EnsureLive();
        if (amountBdt > 0 && amountBdt > OutstandingBdt + Tolerance)
            throw new InvalidOperationException($"Cannot adjust more than the {Math.Max(0, OutstandingBdt):N2} BDT outstanding.");
        if (AdjustedBdt + amountBdt < -Tolerance)
            throw new InvalidOperationException("Cannot undo more than has been adjusted on this bill.");

        AdjustedBdt = Math.Max(0, Math.Round(AdjustedBdt + amountBdt, 2));
    }

    /// <summary>
    /// Withdraws the bill — the deal was settled in cash after all, or the purchase/sale it was
    /// raised for was removed. Only possible before anything was paid or adjusted.
    /// </summary>
    public void Cancel()
    {
        if (IsCancelled)
            return;
        if (HasSettlements)
        {
            throw new InvalidOperationException(
                "This bill already has payments or adjustments. Void them first, or keep the bill open.");
        }

        IsCancelled = true;
    }

    private void EnsureLive()
    {
        if (IsCancelled)
            throw new InvalidOperationException("This bill has been cancelled.");
    }
}
