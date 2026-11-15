using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Events;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Internal;

/// <summary>
/// Posts a <b>manually recorded</b> expense that is tagged to an animal onto that animal's cost
/// ledger, exactly once.
/// </summary>
/// <remarks>
/// <para>
/// Every automated posting (feed confirmation, treatment, vaccination, purchase, sale, overhead
/// split…) already reaches the ledger through its own dedicated handler, so this handler ignores
/// any transaction that is automated or carries a source module. Only a hand-entered expense —
/// which otherwise never reached the animal's ledger at all — is posted here.
/// </para>
/// <para>
/// Where the expense lands:
/// <list type="bullet">
/// <item>Veterinary / medicine → the vet bucket (accumulated, like the treatment handlers).</item>
/// <item>Labour → the labour bucket; utilities, transport, miscellaneous and feed → the overhead
/// bucket. These buckets are <i>recomputed</i> from <see cref="AnimalOverheadAllocation"/> rows
/// whenever overhead is allocated (and the feed bucket from feed allocations), so adding to them
/// directly would be silently wiped. The expense is written as a single 100 % allocation row for
/// the animal instead and the buckets are recomputed from the table: idempotent (a row already
/// existing for the transaction is never written twice) and it survives every later refresh.
/// A manual feed purchase goes to overhead because the feed bucket mirrors feed actually eaten.</item>
/// <item>Anything else (interest, owner drawings) is not an animal cost and is ignored.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class AnimalCostLedgerUpdater : INotificationHandler<FinancialTransactionCreatedEvent>
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalOverheadAllocationRepository _allocations;
    private readonly IUnitOfWork _unitOfWork;

    public AnimalCostLedgerUpdater(
        IFinancialTransactionRepository transactions,
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalOverheadAllocationRepository allocations,
        IUnitOfWork unitOfWork)
    {
        _transactions = transactions;
        _ledgerRepository = ledgerRepository;
        _allocations = allocations;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(FinancialTransactionCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.AnimalId is not { } animalId || animalId == Guid.Empty)
            return;

        // The event carries no provenance, so read the posting itself.
        var transaction = await _transactions.GetByIdAsync(notification.TransactionId, cancellationToken).ConfigureAwait(false);
        if (transaction is null || !IsManualAnimalExpense(transaction))
            return;

        if (IsVetCategory(transaction.Category))
        {
            await SyncManualVetAsync(_ledgerRepository, transaction, cancellationToken).ConfigureAwait(false);
            await _transactions.UpdateAsync(transaction, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (BucketFor(transaction.Category) is null)
            return;

        var existing = await _allocations.GetBySourceTransactionAsync(transaction.Id, cancellationToken).ConfigureAwait(false);
        if (existing.Count == 0 && BuildDirectAllocation(transaction) is { } row)
        {
            _allocations.AddRange([row]);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var ledgerToRefresh = await _ledgerRepository.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (ledgerToRefresh is null)
            return;

        var totals = await _allocations.GetTotalsForAnimalAsync(animalId, cancellationToken).ConfigureAwait(false);
        ledgerToRefresh.UpdateLaborCost(totals.LabourBdt);
        ledgerToRefresh.UpdateOverheadCost(totals.OverheadBdt);
        _ledgerRepository.Update(ledgerToRefresh);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A hand-entered expense: not produced by any module (those set <c>IsAutomated</c> and a
    /// source module and reach the ledger through their own handlers), not deleted.
    /// </summary>
    internal static bool IsManualAnimalExpense(FinancialTransaction transaction) =>
        transaction.Type == TransactionType.Expense
        && !transaction.IsAutomated
        && string.IsNullOrWhiteSpace(transaction.SourceModule)
        && !transaction.IsDeleted
        && transaction.AnimalId.HasValue;

    /// <summary>
    /// The single 100 % labour/overhead allocation row that carries a manual animal-tagged expense,
    /// or null when the expense is not one (wrong type, vet category, or not an animal cost).
    /// Shared with <c>OverheadAllocationService.ReallocateTransactionAsync</c> so edits rewrite it.
    /// </summary>
    internal static AnimalOverheadAllocation? BuildDirectAllocation(FinancialTransaction transaction)
    {
        if (!IsManualAnimalExpense(transaction) || IsVetCategory(transaction.Category))
            return null;
        if (BucketFor(transaction.Category) is not { } bucket)
            return null;

        var date = DateOnly.FromDateTime(transaction.TransactionDate);
        return AnimalOverheadAllocation.Create(
            tenantId: transaction.TenantId,
            animalId: transaction.AnimalId!.Value,
            farmId: transaction.FarmId,
            sourceTransactionId: transaction.Id,
            category: transaction.Category,
            bucket: bucket,
            periodStart: date,
            periodEnd: date,
            allocatedAmountBdt: transaction.AmountBdt,
            method: OverheadAllocationMethod.PerHeadDay,
            headDays: 1m,
            weightAtAllocationKg: 0m,
            shareFactor: 1m,
            headCountAtAllocation: 1);
    }

    /// <summary>
    /// Makes an animal's vet bucket reflect a hand-entered vet/medicine expense exactly once.
    /// </summary>
    /// <remarks>
    /// The vet bucket is accumulated (treatments and vaccinations add to it directly), so it cannot
    /// be recomputed from a table like labour/overhead. Instead the transaction records what it has
    /// posted (<see cref="FinancialTransaction.LedgerVetPostedAnimalId"/> / <c>LedgerVetPostedBdt</c>).
    /// This takes that amount back out and puts the current one in — so creation, edits (amount,
    /// category, animal), deletes (<paramref name="removed"/>) and the historical backfill all go
    /// through the same arithmetic, and running it twice changes nothing. An expense entered before
    /// the marker existed has nothing recorded, so nothing is subtracted for it.
    /// Updates the ledgers and the transaction's marker; the caller saves.
    /// </remarks>
    /// <returns>True when a ledger was changed.</returns>
    internal static async Task<bool> SyncManualVetAsync(
        IAnimalCostLedgerRepository ledgers,
        FinancialTransaction transaction,
        CancellationToken cancellationToken,
        bool removed = false)
    {
        (Guid AnimalId, decimal AmountBdt)? posted =
            transaction.LedgerVetPostedAnimalId is { } pa && transaction.LedgerVetPostedBdt is { } pb ? (pa, pb) : null;
        (Guid AnimalId, decimal AmountBdt)? wanted =
            !removed && IsManualAnimalExpense(transaction) && IsVetCategory(transaction.Category)
                ? (transaction.AnimalId!.Value, transaction.AmountBdt)
                : null;

        if (posted == wanted)
            return false;

        var deltas = new System.Collections.Generic.Dictionary<Guid, decimal>();
        if (posted is { } p) deltas[p.AnimalId] = -p.AmountBdt;
        if (wanted is { } w) deltas[w.AnimalId] = (deltas.TryGetValue(w.AnimalId, out var d) ? d : 0m) + w.AmountBdt;

        var changed = false;
        var wantedPosted = wanted is null;
        foreach (var (animalId, delta) in deltas)
        {
            var ledger = await ledgers.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
            if (ledger is null)
                continue; // Created when the animal is registered; nothing to post to.

            if (wanted is { } target && target.AnimalId == animalId)
                wantedPosted = true;
            if (delta == 0m)
                continue;

            ledger.AdjustVetCost(delta);
            ledgers.Update(ledger);
            changed = true;
        }

        // Only claim what actually landed: if the target animal has no ledger, leave it unposted so
        // a later backfill can post it.
        if (wantedPosted)
            transaction.MarkLedgerVetPosting(wanted?.AnimalId, wanted?.AmountBdt);
        else
            transaction.MarkLedgerVetPosting(null, null);

        return changed;
    }

    internal static bool IsVetCategory(TransactionCategory category) =>
        category is TransactionCategory.VeterinaryCost or TransactionCategory.MedicineCost;

    private static OverheadBucket? BucketFor(TransactionCategory category) => category switch
    {
        TransactionCategory.FeedCost => OverheadBucket.Overhead,
        _ => AnimalOverheadAllocation.BucketFor(category),
    };
}
