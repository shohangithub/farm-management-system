using Farm360.Domain.Common;

namespace Farm360.Domain.Tenancy;

/// <summary>
/// Immutable record of a tenant's subscription purchase, renewal, or trial initiation.
/// Provides transaction audit history, billing reconciliation, and invoice generation.
/// </summary>
public sealed class TenantSubscriptionRecord : BaseEntity
{
    private TenantSubscriptionRecord() { }

    private TenantSubscriptionRecord(
        Guid id,
        Guid tenantId,
        SubscriptionTier tier,
        SubscriptionBillingCycle billingCycle,
        decimal amount,
        string currency,
        DateTime startedAtUtc,
        DateTime? expiresAtUtc,
        string paymentMethod,
        string? paymentReference,
        string status,
        string invoiceNumber,
        string? notes) : base(id)
    {
        TenantId = tenantId;
        Tier = tier;
        BillingCycle = billingCycle;
        Amount = amount;
        Currency = currency;
        StartedAtUtc = startedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        PaymentMethod = paymentMethod;
        PaymentReference = paymentReference;
        Status = status;
        InvoiceNumber = invoiceNumber;
        Notes = notes;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public SubscriptionTier Tier { get; private set; }
    public SubscriptionBillingCycle BillingCycle { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "BDT";
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string PaymentMethod { get; private set; } = string.Empty;
    public string? PaymentReference { get; private set; }
    public string Status { get; private set; } = "Completed";
    public string InvoiceNumber { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static TenantSubscriptionRecord Create(
        Guid tenantId,
        SubscriptionTier tier,
        SubscriptionBillingCycle billingCycle,
        decimal amount,
        string currency,
        DateTime startedAtUtc,
        DateTime? expiresAtUtc,
        string paymentMethod,
        string? paymentReference,
        string? notes = null,
        string status = "Completed")
    {
        var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        return new TenantSubscriptionRecord(
            Guid.NewGuid(),
            tenantId,
            tier,
            billingCycle,
            amount,
            currency,
            startedAtUtc,
            expiresAtUtc,
            paymentMethod,
            paymentReference,
            status,
            invoiceNumber,
            notes);
    }

    /// <summary>
    /// Confirms a <c>Pending</c> record once the payment has actually been verified -- by an
    /// admin reviewing a manually-reported reference, or by a payment gateway's callback.
    /// </summary>
    public void MarkCompleted(string? verifiedReference = null)
    {
        if (Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot complete a subscription record in '{Status}' status.");
        }

        Status = "Completed";
        if (!string.IsNullOrWhiteSpace(verifiedReference))
        {
            PaymentReference = verifiedReference;
        }
    }

    /// <summary>Rejects a <c>Pending</c> record -- the reported payment did not check out.</summary>
    public void MarkRejected(string? reason = null)
    {
        if (Status != "Pending")
        {
            throw new InvalidOperationException($"Cannot reject a subscription record in '{Status}' status.");
        }

        Status = "Rejected";
        Notes = string.IsNullOrWhiteSpace(reason) ? Notes : $"{Notes} | Rejected: {reason}";
    }
}
