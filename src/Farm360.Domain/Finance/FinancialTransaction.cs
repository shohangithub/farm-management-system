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
    public bool IsAutomated { get; private set; }
    public string? SourceModule { get; private set; }

    /// <summary>
    /// What the posting was settled against. Cash for real money movements; Inventory or
    /// Livestock for non-cash postings (stock consumed, animal sold or died). See <see cref="SettlementAccount"/>.
    /// </summary>
    public SettlementAccount Settlement { get; private set; } = SettlementAccount.Cash;

    /// <summary>
    /// The business document that produced an automated posting (purchase order, feeding entry,
    /// loan, animal…). Together with the category it makes automated posting idempotent: the same
    /// source can never be posted twice.
    /// </summary>
    public Guid? SourceDocumentId { get; private set; }

    /// <summary>
    /// For a hand-entered vet/medicine expense tagged to an animal: the animal and amount this
    /// posting has actually added to that animal's cost-ledger vet bucket. Null = nothing posted.
    /// Recording what was posted (rather than inferring it) is what lets edits, deletes and the
    /// backfill move exactly that amount, and makes re-running the backfill a no-op.
    /// </summary>
    public Guid? LedgerVetPostedAnimalId { get; private set; }

    /// <inheritdoc cref="LedgerVetPostedAnimalId"/>
    public decimal? LedgerVetPostedBdt { get; private set; }

    /// <summary>
    /// The money account (cash in hand, bank, mobile wallet) a cash posting moved money in or out of.
    /// Null only on non-cash postings, or on a cash posting not yet saved: the farm's default account
    /// is filled in on save, so null on a cash row always means "the default account".
    /// </summary>
    public Guid? MoneyAccountId { get; private set; }

    /// <summary>
    /// The other half of a paired posting: the opposite leg of a transfer between money accounts, or
    /// the partner capital contribution booked for a spend the partner paid directly (and back).
    /// </summary>
    public Guid? LinkedTransactionId { get; private set; }

    /// <summary>One leg of a transfer between the farm's own money accounts (no farm-level cash moves).</summary>
    public bool IsAccountTransfer => Category == TransactionCategory.AccountTransfer;

    /// <summary>
    /// True when the posting moves money in or out of the farm's cash book. A transfer between two
    /// of the farm's own accounts moves money between accounts but not in or out of the farm.
    /// </summary>
    public bool AffectsCash => Settlement == SettlementAccount.Cash && !IsAccountTransfer;

    /// <summary>True when the posting changes the balance of a money account (transfers included).</summary>
    public bool MovesMoneyAccount => Settlement == SettlementAccount.Cash;

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
        Guid? shedId = null,
        bool isAutomated = false,
        string? sourceModule = null,
        SettlementAccount settlement = SettlementAccount.Cash,
        Guid? sourceDocumentId = null,
        Guid? moneyAccountId = null)
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
            ShedId = shedId,
            IsAutomated = isAutomated,
            SourceModule = sourceModule,
            Settlement = settlement,
            SourceDocumentId = sourceDocumentId,
            MoneyAccountId = settlement == SettlementAccount.Cash ? moneyAccountId : null
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
        Guid? shedId = null,
        string? referenceId = null)
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
        if (referenceId != null)
        {
            ReferenceId = referenceId.Trim();
        }
    }

    /// <summary>
    /// Re-states an automated posting when its source document changes (price corrected, entry
    /// adjusted). Keeps the same row so the posting stays idempotent and auditable.
    /// </summary>
    public void Revise(decimal amountBdt, DateTime transactionDate, string description, string notes, SettlementAccount settlement)
    {
        if (amountBdt < 0)
            throw new ArgumentException("Transaction amount cannot be negative.", nameof(amountBdt));

        AmountBdt = amountBdt;
        TransactionDate = transactionDate;
        Description = description?.Trim() ?? string.Empty;
        Notes = notes?.Trim() ?? string.Empty;
        Settlement = settlement;
        if (settlement != SettlementAccount.Cash)
            MoneyAccountId = null;
    }

    /// <summary>
    /// Puts a cash posting in a money account. Ignored for non-cash postings (stock consumed, credit
    /// deals): they move no money until paid, and the payment is its own posting.
    /// </summary>
    public void AssignMoneyAccount(Guid? moneyAccountId)
    {
        MoneyAccountId = Settlement == SettlementAccount.Cash ? moneyAccountId : null;
    }

    /// <summary>Pairs this posting with its other half (see <see cref="LinkedTransactionId"/>); null unpairs it.</summary>
    public void LinkTo(Guid? transactionId)
    {
        LinkedTransactionId = transactionId;
    }

    /// <summary>Links a legacy posting to its source document (used by the ledger repair tool).</summary>
    public void LinkSource(Guid sourceDocumentId, string? sourceModule)
    {
        SourceDocumentId = sourceDocumentId;
        if (!string.IsNullOrWhiteSpace(sourceModule))
            SourceModule = sourceModule;
    }

    /// <summary>Records what this expense has put into an animal's vet bucket (null clears it).</summary>
    public void MarkLedgerVetPosting(Guid? animalId, decimal? amountBdt)
    {
        LedgerVetPostedAnimalId = animalId;
        LedgerVetPostedBdt = animalId.HasValue ? amountBdt : null;
    }
}
