using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance.Accounting;

public enum BalanceSheetEntryType
{
    /// <summary>The position on the books-start date, from the last audited balance sheet. One-sided.</summary>
    Opening = 0,

    /// <summary>A non-cash year-end entry (provision, accrual, prepayment). Its other side is profit.</summary>
    Adjustment = 1,
}

/// <summary>
/// A balance-sheet figure that is not a cash movement: an opening balance brought in from the last
/// audited statement, or a year-end adjustment. Never posted to the cash book.
/// </summary>
/// <remarks>
/// Opening entries are deliberately one-sided: a complete opening position nets to zero, and an
/// incomplete one shows up as a Difference on the balance sheet instead of being hidden by a plug.
/// Adjustments are two-sided (balance-sheet account against a profit &amp; loss account), so they
/// always balance; with a <see cref="ReversalDate"/> they unwind automatically, so an accrued bill is
/// not charged twice when it is later paid.
/// </remarks>
public sealed class BalanceSheetEntry : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public DateOnly EntryDate { get; private set; }
    public BalanceSheetEntryType EntryType { get; private set; }

    /// <summary>The balance-sheet account the amount adds to (chart of accounts code).</summary>
    public string AccountCode { get; private set; } = string.Empty;

    /// <summary>Signed: positive increases the line in its natural direction, negative reduces it.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Adjustment only: the profit &amp; loss account on the other side.</summary>
    public string? ProfitAccountCode { get; private set; }

    /// <summary>Adjustment only: when set, the entry is reversed on this date.</summary>
    public DateOnly? ReversalDate { get; private set; }

    public string Description { get; private set; } = string.Empty;
    public string? Notes { get; private set; }

    private BalanceSheetEntry() { } // EF Core

    private BalanceSheetEntry(Guid tenantId, Guid farmId, string code) : base(Guid.NewGuid(), tenantId)
    {
        FarmId = farmId;
        Code = code;
    }

    public static BalanceSheetEntry Create(
        Guid tenantId,
        Guid farmId,
        string code,
        BalanceSheetEntryType entryType,
        DateOnly entryDate,
        string accountCode,
        decimal amount,
        string description,
        string? profitAccountCode = null,
        DateOnly? reversalDate = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Code is required.", nameof(code));

        var entry = new BalanceSheetEntry(tenantId, farmId, code.Trim());
        entry.Update(entryType, entryDate, accountCode, amount, description, profitAccountCode, reversalDate, notes);
        return entry;
    }

    public void Update(
        BalanceSheetEntryType entryType,
        DateOnly entryDate,
        string accountCode,
        decimal amount,
        string description,
        string? profitAccountCode = null,
        DateOnly? reversalDate = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
            throw new ArgumentException("Choose the balance-sheet line.", nameof(accountCode));
        if (amount == 0)
            throw new ArgumentException("The amount cannot be zero.", nameof(amount));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A description is required.", nameof(description));

        if (entryType == BalanceSheetEntryType.Adjustment)
        {
            if (string.IsNullOrWhiteSpace(profitAccountCode))
                throw new ArgumentException("An adjustment needs the profit & loss account on its other side.", nameof(profitAccountCode));
            if (reversalDate is { } reversal && reversal <= entryDate)
                throw new ArgumentException("The reversal date must be after the entry date.", nameof(reversalDate));
        }
        else
        {
            // Opening entries are the position on one date; they have no P&L side and never reverse.
            profitAccountCode = null;
            reversalDate = null;
        }

        EntryType = entryType;
        EntryDate = entryDate;
        AccountCode = accountCode.Trim();
        Amount = Math.Round(amount, 2);
        ProfitAccountCode = profitAccountCode?.Trim();
        ReversalDate = reversalDate;
        Description = description.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }
}
