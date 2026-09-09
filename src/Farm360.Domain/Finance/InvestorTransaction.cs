using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// Child entity representing a financial transaction with an investor:
/// capital injection, capital withdrawal, or profit distribution.
/// </summary>
public sealed class InvestorTransaction : AuditableEntity
{
    public Guid InvestorId { get; private set; }
    public Guid FarmId { get; private set; }
    public InvestorTransactionType Type { get; private set; }
    public decimal AmountBdt { get; private set; }
    public DateTime TransactionDate { get; private set; }
    public string? ReferenceId { get; private set; }
    public string? Notes { get; private set; }
    public Guid? FinancialTransactionId { get; private set; }

    private InvestorTransaction() { } // For EF Core

    public static InvestorTransaction Create(
        Guid tenantId,
        Guid investorId,
        Guid farmId,
        InvestorTransactionType type,
        decimal amountBdt,
        DateTime transactionDate,
        string? referenceId = null,
        string? notes = null,
        Guid? financialTransactionId = null)
    {
        if (amountBdt <= 0)
            throw new ArgumentException("Transaction amount must be greater than zero.", nameof(amountBdt));

        var tx = new InvestorTransaction
        {
            Id = Guid.NewGuid(),
            InvestorId = investorId,
            FarmId = farmId,
            Type = type,
            AmountBdt = amountBdt,
            TransactionDate = transactionDate,
            ReferenceId = referenceId?.Trim(),
            Notes = notes?.Trim(),
            FinancialTransactionId = financialTransactionId
        };

        tx.SetTenantId(tenantId);
        return tx;
    }
}
