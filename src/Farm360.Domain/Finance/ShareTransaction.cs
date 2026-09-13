using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// ShareTransaction — Audit entity recording every share issuance, buyback, transfer, or valuation change.
/// </summary>
public sealed class ShareTransaction : AuditableEntity, IAggregateRoot
{
    public Guid? ShareHoldingId { get; private set; }
    public Guid InvestorId { get; private set; }
    public Guid FarmId { get; private set; }
    public ShareTransactionType Type { get; private set; }
    public int ShareCount { get; private set; }
    public decimal PricePerShareBdt { get; private set; }
    public decimal TotalAmountBdt { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public Guid? CounterpartyInvestorId { get; private set; }
    public string? ReferenceId { get; private set; }
    public string? Notes { get; private set; }
    public Guid? FinancialTransactionId { get; private set; }

    private ShareTransaction() { } // For EF Core

    public static ShareTransaction Create(
        Guid tenantId,
        Guid farmId,
        Guid investorId,
        ShareTransactionType type,
        int shareCount,
        decimal pricePerShareBdt,
        DateTime transactionDate,
        Guid? shareHoldingId = null,
        Guid? counterpartyInvestorId = null,
        string? referenceId = null,
        string? notes = null,
        Guid? financialTransactionId = null)
    {
        if (shareCount <= 0)
            throw new ArgumentException("Share count must be greater than zero.", nameof(shareCount));
        if (pricePerShareBdt < 0)
            throw new ArgumentException("Price per share cannot be negative.", nameof(pricePerShareBdt));

        var tx = new ShareTransaction
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            InvestorId = investorId,
            ShareHoldingId = shareHoldingId,
            Type = type,
            ShareCount = shareCount,
            PricePerShareBdt = pricePerShareBdt,
            TotalAmountBdt = Math.Round(shareCount * pricePerShareBdt, 2),
            TransactionDate = transactionDate,
            CounterpartyInvestorId = counterpartyInvestorId,
            ReferenceId = referenceId?.Trim(),
            Notes = notes?.Trim(),
            FinancialTransactionId = financialTransactionId
        };

        tx.SetTenantId(tenantId);
        return tx;
    }
}
