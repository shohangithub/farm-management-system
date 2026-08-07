using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

public class FinancialTransaction : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    
    public TransactionType Type { get; private set; }
    public TransactionCategory Category { get; private set; }
    public decimal AmountBdt { get; private set; }
    public DateTime TransactionDate { get; private set; }
    
    public string ReferenceId { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;

    private FinancialTransaction() { } // For EF Core

    private FinancialTransaction(
        Guid id,
        Guid tenantId,
        Guid farmId,
        TransactionType type,
        TransactionCategory category,
        decimal amountBdt,
        DateTime transactionDate,
        string referenceId,
        string notes) : base(id, tenantId)
    {
        FarmId = farmId;
        Type = type;
        Category = category;
        AmountBdt = amountBdt;
        TransactionDate = transactionDate;
        ReferenceId = referenceId;
        Notes = notes;
    }

    public static FinancialTransaction Create(
        Guid tenantId,
        Guid farmId,
        TransactionType type,
        TransactionCategory category,
        decimal amountBdt,
        DateTime transactionDate,
        string referenceId = "",
        string notes = "")
    {
        if (amountBdt < 0)
            throw new ArgumentException("Transaction amount cannot be negative.", nameof(amountBdt));

        return new FinancialTransaction(
            Guid.NewGuid(),
            tenantId,
            farmId, 
            type, 
            category, 
            amountBdt, 
            transactionDate, 
            referenceId, 
            notes);
    }
}
