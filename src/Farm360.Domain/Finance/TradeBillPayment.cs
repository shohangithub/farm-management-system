using System;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance;

/// <summary>
/// One payment made to a supplier, or collected from a customer, against a <see cref="TradeBill"/>.
/// Its id is the source document of the cash posting (Supplier Payment / Customer Receipt) it produced.
/// </summary>
public sealed class TradeBillPayment : AuditableEntity
{
    public Guid BillId { get; private set; }
    public Guid FarmId { get; private set; }
    public DateOnly PaymentDate { get; private set; }
    public decimal AmountBdt { get; private set; }

    /// <summary>How it was paid — Cash, Bank, bKash, Nagad, Cheque… (for reference; all are cash &amp; bank).</summary>
    public string Method { get; private set; } = "Cash";

    public string? Reference { get; private set; }
    public string? Notes { get; private set; }

    private TradeBillPayment() { } // EF Core

    public static TradeBillPayment Create(
        Guid tenantId,
        Guid billId,
        Guid farmId,
        DateOnly paymentDate,
        decimal amountBdt,
        string? method,
        string? reference,
        string? notes)
    {
        if (amountBdt <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amountBdt));

        var payment = new TradeBillPayment
        {
            Id = Guid.NewGuid(),
            BillId = billId,
            FarmId = farmId,
            PaymentDate = paymentDate,
            AmountBdt = Math.Round(amountBdt, 2),
            Method = string.IsNullOrWhiteSpace(method) ? "Cash" : method.Trim(),
            Reference = reference?.Trim(),
            Notes = notes?.Trim(),
        };

        payment.SetTenantId(tenantId);
        return payment;
    }
}
