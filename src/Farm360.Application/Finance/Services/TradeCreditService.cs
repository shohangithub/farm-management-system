using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.Finance.Services;

/// <summary>Who a credit deal is with and when it is due.</summary>
public sealed record CreditTerms(
    string PartyName,
    string? PartyPhone = null,
    DateOnly? DueDate = null,
    decimal PaidNowBdt = 0m,
    string? PaymentMethod = null,
    string? Notes = null,
    Guid? SupplierId = null);

/// <summary>
/// Payables and receivables. A purchase or sale is booked by its own module as usual; putting it on
/// credit raises a <see cref="TradeBill"/> and re-settles that posting against Accounts Payable /
/// Receivable, so the cost or revenue stays on the deal date while cash moves only when paid.
/// </summary>
/// <remarks>
/// Every money movement is a ledger posting keyed on its own document (payment id, bill id), so
/// nothing here can double-post. Nothing here saves; callers commit with their unit of work.
/// </remarks>
public interface ITradeCreditService
{
    /// <summary>Puts a posting on credit. The posting may be new (same unit of work) or already saved.</summary>
    Task<TradeBill> OpenAsync(FinancialTransaction posting, CreditTerms terms, CancellationToken cancellationToken = default);

    /// <summary>Puts the posting a module produced for a source document on credit.</summary>
    Task<TradeBill> OpenForSourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CreditTerms terms, CancellationToken cancellationToken = default);

    Task<TradeBillPayment> RecordPaymentAsync(TradeBill bill, decimal amountBdt, DateOnly date, string? method, string? reference, string? notes, CancellationToken cancellationToken = default);

    Task VoidPaymentAsync(TradeBill bill, TradeBillPayment payment, CancellationToken cancellationToken = default);

    /// <summary>Clears what is left without cash — bad debt (receivable) or a waiver (payable).</summary>
    Task WriteOffAsync(TradeBill bill, decimal? amountBdt, DateOnly date, string? reason, CancellationToken cancellationToken = default);

    /// <summary>Withdraws an unpaid bill; the deal is treated as paid in cash on its date.</summary>
    Task CancelAsync(TradeBill bill, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stock returned against a purchase order bought on credit reduces what is owed instead of
    /// being refunded in cash. Returns the settlement the return posting must use.
    /// </summary>
    Task<SettlementAccount> SettleReturnAsync(Guid farmId, Guid purchaseOrderId, Guid returnId, decimal amountBdt, CancellationToken cancellationToken = default);
}

public sealed class TradeCreditService : ITradeCreditService
{
    public const string Module = "Credit";

    /// <summary>Usual terms when none are given.</summary>
    public const int DefaultCreditDays = 30;

    private readonly ITradeBillRepository _bills;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly ILedgerPostingService _posting;

    public TradeCreditService(ITradeBillRepository bills, IFinancialTransactionRepository transactions, ILedgerPostingService posting)
    {
        _bills = bills;
        _transactions = transactions;
        _posting = posting;
    }

    public async Task<TradeBill> OpenAsync(FinancialTransaction posting, CreditTerms terms, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(terms);

        var kind = AccountChart.CreditKind(posting)
            ?? throw new InvalidOperationException(
                $"'{AccountChart.For(posting.Category).Name}' cannot be put on credit. Only purchases and sales can.");

        var expected = kind == TradeBillKind.Payable ? SettlementAccount.Payable : SettlementAccount.Receivable;
        if (posting.Settlement != SettlementAccount.Cash && posting.Settlement != expected)
            throw new InvalidOperationException("This entry is not a cash purchase or sale, so it cannot be put on credit.");

        // Hand-entered entries have no source document; they become their own.
        if (posting.SourceDocumentId is null)
            posting.LinkSource(posting.Id, null);
        var sourceId = posting.SourceDocumentId!.Value;

        if (await _bills.FindBySourceAsync(posting.FarmId, sourceId, posting.Category, cancellationToken) is not null)
            throw new InvalidOperationException("This purchase or sale is already on credit. Record a payment against its bill instead.");

        var billDate = DateOnly.FromDateTime(posting.TransactionDate);
        var bill = TradeBill.Create(
            posting.TenantId,
            posting.FarmId,
            kind,
            terms.PartyName,
            sourceId,
            posting.Category,
            posting.AmountBdt,
            billDate,
            terms.DueDate ?? billDate.AddDays(DefaultCreditDays),
            AccountChart.VoucherNo(posting),
            AccountChart.Describe(posting),
            posting.SourceModule,
            terms.SupplierId,
            terms.PartyPhone,
            terms.Notes);
        _bills.Add(bill);

        if (posting.Settlement != expected)
        {
            posting.Revise(posting.AmountBdt, posting.TransactionDate, posting.Description, posting.Notes, expected);
            await _transactions.UpdateAsync(posting, cancellationToken);
        }

        if (terms.PaidNowBdt > 0)
        {
            await RecordPaymentAsync(bill, terms.PaidNowBdt, billDate, terms.PaymentMethod,
                bill.ReferenceNo, "Paid on the day of the deal", cancellationToken);
        }

        return bill;
    }

    public async Task<TradeBill> OpenForSourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CreditTerms terms, CancellationToken cancellationToken = default)
    {
        var posting = await _transactions.FindBySourceAsync(farmId, sourceDocumentId, category, cancellationToken)
            ?? throw new InvalidOperationException("The purchase or sale has not been posted to the ledger yet, so it cannot be put on credit.");
        return await OpenAsync(posting, terms, cancellationToken);
    }

    public async Task<TradeBillPayment> RecordPaymentAsync(
        TradeBill bill, decimal amountBdt, DateOnly date, string? method, string? reference, string? notes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);
        if (date < bill.BillDate)
            throw new InvalidOperationException($"A payment cannot be dated before the bill ({bill.BillDate:dd MMM yyyy}).");

        bill.ApplyPayment(amountBdt);
        var payment = TradeBillPayment.Create(bill.TenantId, bill.Id, bill.FarmId, date, amountBdt, method, reference, notes);
        _bills.AddPayment(payment);
        _bills.Update(bill);

        var payable = bill.Kind == TradeBillKind.Payable;
        await _posting.PostAsync(new LedgerPosting(
            bill.TenantId,
            bill.FarmId,
            payable ? TransactionCategory.SupplierPayment : TransactionCategory.CustomerReceipt,
            payment.AmountBdt,
            date.ToDateTime(TimeOnly.MinValue),
            payment.Id,
            Module,
            payable
                ? $"Paid to {bill.PartyName} - {bill.ReferenceNo}"
                : $"Received from {bill.PartyName} - {bill.ReferenceNo}",
            Notes: $"{payment.Method}{(string.IsNullOrWhiteSpace(reference) ? string.Empty : $" ({reference})")} against: {bill.Description}",
            ReferenceId: string.IsNullOrWhiteSpace(reference) ? bill.ReferenceNo : reference), cancellationToken);

        return payment;
    }

    public async Task VoidPaymentAsync(TradeBill bill, TradeBillPayment payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(payment);
        if (payment.BillId != bill.Id)
            throw new InvalidOperationException("That payment belongs to a different bill.");

        bill.ReversePayment(payment.AmountBdt);
        _bills.Update(bill);
        _bills.RemovePayment(payment);

        var category = bill.Kind == TradeBillKind.Payable ? TransactionCategory.SupplierPayment : TransactionCategory.CustomerReceipt;
        await _posting.ReverseAsync(bill.FarmId, payment.Id, category, cancellationToken);
    }

    public async Task WriteOffAsync(TradeBill bill, decimal? amountBdt, DateOnly date, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);
        var amount = Math.Round(amountBdt ?? bill.OutstandingBdt, 2);
        if (amount <= 0)
            throw new InvalidOperationException("Nothing is outstanding on this bill.");
        if (date < bill.BillDate)
            throw new InvalidOperationException($"A write-off cannot be dated before the bill ({bill.BillDate:dd MMM yyyy}).");

        bill.Adjust(amount);
        _bills.Update(bill);

        // One write-off posting per bill, accumulated, so it stays idempotent on the bill id.
        var receivable = bill.Kind == TradeBillKind.Receivable;
        var category = receivable ? TransactionCategory.BadDebt : TransactionCategory.OtherIncome;
        var previous = await _transactions.FindBySourceAsync(bill.FarmId, bill.Id, category, cancellationToken);
        var why = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" - {reason.Trim()}";

        await _posting.PostAsync(new LedgerPosting(
            bill.TenantId,
            bill.FarmId,
            category,
            (previous?.AmountBdt ?? 0m) + amount,
            date.ToDateTime(TimeOnly.MinValue),
            bill.Id,
            Module,
            receivable
                ? $"Bad debt written off - {bill.PartyName} ({bill.ReferenceNo})"
                : $"Balance waived by {bill.PartyName} ({bill.ReferenceNo})",
            Settlement: bill.Settlement,
            Notes: (receivable ? "Uncollectable customer balance" : "Supplier discount / waiver") + why,
            ReferenceId: bill.ReferenceNo), cancellationToken);
    }

    public async Task CancelAsync(TradeBill bill, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bill);
        bill.Cancel();
        _bills.Update(bill);

        var posting = await _transactions.FindBySourceAsync(bill.FarmId, bill.SourceDocumentId, bill.Category, cancellationToken);
        if (posting is not null && posting.Settlement == bill.Settlement)
        {
            posting.Revise(posting.AmountBdt, posting.TransactionDate, posting.Description, posting.Notes, SettlementAccount.Cash);
            await _transactions.UpdateAsync(posting, cancellationToken);
        }
    }

    public async Task<SettlementAccount> SettleReturnAsync(Guid farmId, Guid purchaseOrderId, Guid returnId, decimal amountBdt, CancellationToken cancellationToken = default)
    {
        var bill = await _bills.FindBySourceAsync(farmId, purchaseOrderId, TransactionCategory.InventoryPurchase, cancellationToken);
        var previous = await _transactions.FindBySourceAsync(farmId, returnId, TransactionCategory.InventoryPurchaseReturn, cancellationToken);
        var alreadyCredited = previous?.Settlement == SettlementAccount.Payable ? previous.AmountBdt : 0m;

        if (bill is null)
            return SettlementAccount.Cash;

        // Credit the bill if what is still owed (plus anything this return already took) covers it;
        // otherwise the supplier refunds in cash.
        if (bill.OutstandingBdt + alreadyCredited >= amountBdt - TradeBill.Tolerance)
        {
            bill.Adjust(amountBdt - alreadyCredited);
            _bills.Update(bill);
            return SettlementAccount.Payable;
        }

        if (alreadyCredited > 0)
        {
            bill.Adjust(-alreadyCredited);
            _bills.Update(bill);
        }

        return SettlementAccount.Cash;
    }
}
