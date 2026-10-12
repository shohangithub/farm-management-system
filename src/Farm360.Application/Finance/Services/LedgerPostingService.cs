using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.Finance.Services;

/// <summary>An automated ledger posting requested by a module (inventory, feeding, livestock…).</summary>
/// <param name="SourceDocumentId">The document that caused it. With the category it is the idempotency key.</param>
public sealed record LedgerPosting(
    Guid TenantId,
    Guid FarmId,
    TransactionCategory Category,
    decimal AmountBdt,
    DateTime TransactionDate,
    Guid SourceDocumentId,
    string SourceModule,
    string Description,
    SettlementAccount Settlement = SettlementAccount.Cash,
    string Notes = "",
    string ReferenceId = "",
    Guid? AnimalId = null,
    Guid? BatchId = null,
    Guid? ShedId = null,
    TransactionType? Type = null)
{
    /// <summary>
    /// A stable id for a second posting of the same category from one document (e.g. the stock and
    /// the direct-cash part of one feeding entry). Same inputs always give the same id.
    /// </summary>
    public static Guid SubKey(Guid sourceDocumentId, string purpose)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"{sourceDocumentId:N}:{purpose}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}

/// <summary>
/// The one way automated postings reach the ledger. Guarantees:
/// <list type="bullet">
/// <item>Idempotent — the same (source document, category) is posted at most once; re-posting
/// revises the existing row instead of adding a duplicate.</item>
/// <item>Correct direction — the type comes from the chart of accounts unless overridden.</item>
/// <item>A zero amount removes a previous posting (e.g. a purchase price cleared).</item>
/// <item>Credit-aware — when the source was bought or sold on credit (a live <see cref="TradeBill"/>),
/// the posting stays settled against Payable/Receivable and the bill follows the new amount.</item>
/// </list>
/// Does not save: callers commit together with the business change that caused the posting.
/// </summary>
public interface ILedgerPostingService
{
    Task<FinancialTransaction?> PostAsync(LedgerPosting posting, CancellationToken cancellationToken = default);

    /// <summary>Removes the posting a source produced for a category, if any.</summary>
    Task ReverseAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default);
}

public sealed class LedgerPostingService : ILedgerPostingService
{
    private readonly IFinancialTransactionRepository _repository;
    private readonly ITradeBillRepository? _bills;

    public LedgerPostingService(IFinancialTransactionRepository repository, ITradeBillRepository? bills = null)
    {
        _repository = repository;
        _bills = bills;
    }

    public async Task<FinancialTransaction?> PostAsync(LedgerPosting posting, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(posting);

        var amount = Math.Round(posting.AmountBdt, 2);
        var existing = await _repository
            .FindBySourceAsync(posting.FarmId, posting.SourceDocumentId, posting.Category, cancellationToken);
        var bill = _bills is null
            ? null
            : await _bills.FindBySourceAsync(posting.FarmId, posting.SourceDocumentId, posting.Category, cancellationToken);

        if (amount <= 0)
        {
            if (existing is not null)
                await _repository.DeleteAsync(existing, cancellationToken);
            SourceRemoved(bill);
            return null;
        }

        // Bought or sold on credit: the deal is still owed, whatever the module asked for.
        var settlement = posting.Settlement;
        if (bill is not null && settlement == SettlementAccount.Cash)
        {
            settlement = bill.Settlement;
            bill.Revise(amount, DateOnly.FromDateTime(posting.TransactionDate));
            _bills!.Update(bill);
        }

        if (existing is not null)
        {
            existing.Revise(amount, posting.TransactionDate, posting.Description, posting.Notes, settlement);
            await _repository.UpdateAsync(existing, cancellationToken);
            return existing;
        }

        var transaction = FinancialTransaction.Create(
            posting.TenantId,
            posting.FarmId,
            posting.Type ?? AccountChart.For(posting.Category).Type,
            posting.Category,
            amount,
            posting.TransactionDate,
            referenceId: posting.ReferenceId,
            notes: posting.Notes,
            description: posting.Description,
            animalId: posting.AnimalId,
            batchId: posting.BatchId,
            shedId: posting.ShedId,
            isAutomated: true,
            sourceModule: posting.SourceModule,
            settlement: settlement,
            sourceDocumentId: posting.SourceDocumentId);

        await _repository.AddAsync(transaction, cancellationToken);
        return transaction;
    }

    public async Task ReverseAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.FindBySourceAsync(farmId, sourceDocumentId, category, cancellationToken);
        if (existing is not null)
            await _repository.DeleteAsync(existing, cancellationToken);

        if (_bills is not null)
        {
            var bill = await _bills.FindBySourceAsync(farmId, sourceDocumentId, category, cancellationToken);
            SourceRemoved(bill);
        }
    }

    /// <summary>
    /// The purchase or sale behind a credit bill is gone. An untouched bill is withdrawn; one that
    /// was already (part) paid drops to zero and shows as overpaid — money to recover from the party.
    /// </summary>
    private void SourceRemoved(TradeBill? bill)
    {
        if (bill is null)
            return;

        if (bill.HasSettlements)
            bill.Revise(0m, bill.BillDate);
        else
            bill.Cancel();
        _bills!.Update(bill);
    }
}
