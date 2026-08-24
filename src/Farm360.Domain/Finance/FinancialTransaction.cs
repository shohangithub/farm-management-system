using System;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>
/// Financial Transaction — Aggregate root for all income and expense records.
/// PRD FR-FM-02/03: Manual income/expense recording with entity linking.
/// PRD FR-FM-04/05: Auto-posting from Livestock, Feeding, and Health modules.
/// </summary>
public sealed class FinancialTransaction : AuditableEntity, IAggregateRoot
{
    public Guid FarmId { get; private set; }
    
    public TransactionType Type { get; private set; }
    public TransactionCategory Category { get; private set; }
    public decimal AmountBdt { get; private set; }
    public DateTime TransactionDate { get; private set; }
    
    // ── Entity Links (FR-FM-02/03: optional animal/batch/shed link) ─────────
    public Guid? AnimalId { get; private set; }
    public Guid? BatchId { get; private set; }
    public Guid? ShedId { get; private set; }
    
    public string ReferenceId { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Notes { get; private set; } = string.Empty;

    private FinancialTransaction() { } // For EF Core

    public static FinancialTransaction Create(
        Guid tenantId,
        Guid farmId,
        TransactionType type,
        TransactionCategory category,
        decimal amountBdt,
        DateTime transactionDate,
        string referenceId = "",
        string notes = "",
        string description = "",
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null)
    {
        if (amountBdt < 0)
            throw new ArgumentException("Transaction amount cannot be negative.", nameof(amountBdt));

        var transaction = new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            FarmId = farmId,
            Type = type,
            Category = category,
            AmountBdt = amountBdt,
            TransactionDate = transactionDate,
            ReferenceId = referenceId?.Trim() ?? string.Empty,
            Notes = notes?.Trim() ?? string.Empty,
            Description = description?.Trim() ?? string.Empty,
            AnimalId = animalId,
            BatchId = batchId,
            ShedId = shedId
        };
        
        // Use the protected SetTenantId pattern from AuditableEntity
        transaction.SetTenantId(tenantId);
        
        transaction.RaiseDomainEvent(new Events.FinancialTransactionCreatedEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            transaction.Id,
            tenantId,
            farmId,
            category,
            amountBdt,
            animalId
        ));

        return transaction;
    }
    
    public void UpdateDetails(
        TransactionCategory category,
        decimal amountBdt,
        DateTime transactionDate,
        string description,
        string notes,
        Guid? animalId = null,
        Guid? batchId = null,
        Guid? shedId = null)
    {
        if (amountBdt < 0)
            throw new ArgumentException("Transaction amount cannot be negative.", nameof(amountBdt));
            
        Category = category;
        AmountBdt = amountBdt;
        TransactionDate = transactionDate;
        Description = description?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        AnimalId = animalId;
        BatchId = batchId;
        ShedId = shedId;
    }
}
