using System.Globalization;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Services;

/// <summary>What one exit run changed. All zero on a repeat run.</summary>
public sealed record AnimalExitResult(
    int PlansCancelled,
    int EntriesSkipped,
    int EntriesReversed,
    int GroupEntriesResplit)
{
    public static AnimalExitResult None { get; } = new(0, 0, 0, 0);

    public bool ChangedAnything => PlansCancelled + EntriesSkipped + EntriesReversed + GroupEntriesResplit > 0;
}

/// <summary>
/// Stops feeding — and charging feed to — an animal that left the farm (sold or died).
/// </summary>
public interface IAnimalExitService
{
    /// <summary>
    /// Applies the animal's exit on <paramref name="exitDate"/> to feeding and feed cost. Idempotent:
    /// running it again for the same exit changes nothing. Saves its own work.
    /// </summary>
    /// <param name="reason">Shown on every skipped or reversed entry, e.g. "Animal sold on 13 Nov 2026".</param>
    Task<AnimalExitResult> ProcessExitAsync(Guid animalId, DateOnly exitDate, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies the farm's feeding rules to an animal that left the farm, however late the sale or death
/// was recorded.
/// </summary>
/// <remarks>
/// <para><b>Farm rules.</b> An animal is fed on day D when it was acquired on or before D and D is
/// before its exit date (sale or death date) — see <see cref="AnimalFeedingPresence"/>. On the exit
/// day itself new or pending entries do not feed it; an entry for the exit day already confirmed
/// stays (it really ate that morning).</para>
/// <para><b>Pending entries are plans, not facts</b>, so they are adjusted freely: skipped (an
/// individual plan) or re-planned for the smaller group. <b>Confirmed entries are facts</b>, so they
/// are corrected explicitly and auditably: an individual entry dated after the exit is reversed (its
/// stock comes back with a <see cref="StockTransactionType.FeedConsumptionReversal"/> movement, its
/// ledger posting is removed, its status and reason say why); a group entry keeps its stock and cost
/// — the feed went to the pen — and only its split among the animals actually there changes.</para>
/// </remarks>
public sealed class AnimalExitService : IAnimalExitService
{
    private readonly IFarmClock _clock;

    private readonly IAnimalRepository _animals;
    private readonly IAnimalFeedingPlanRepository _plans;
    private readonly IDailyFeedingEntryRepository _entries;
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IFeedAllocationService _allocationService;
    private readonly IInventoryItemRepository _inventory;
    private readonly IStockTransactionRepository _stockTransactions;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IAnimalCostLedgerRepository _costLedgers;
    private readonly IFarmBusinessRulesProvider _rules;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnimalExitService> _logger;

    public AnimalExitService(
        IAnimalRepository animals,
        IAnimalFeedingPlanRepository plans,
        IDailyFeedingEntryRepository entries,
        IAnimalFeedAllocationRepository allocations,
        IFeedAllocationService allocationService,
        IInventoryItemRepository inventory,
        IStockTransactionRepository stockTransactions,
        ILedgerPostingService ledgerPosting,
        IAnimalCostLedgerRepository costLedgers,
        IFarmBusinessRulesProvider rules,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        ILogger<AnimalExitService> logger,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _animals = animals;
        _plans = plans;
        _entries = entries;
        _allocations = allocations;
        _allocationService = allocationService;
        _inventory = inventory;
        _stockTransactions = stockTransactions;
        _ledgerPosting = ledgerPosting;
        _costLedgers = costLedgers;
        _rules = rules;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>"Animal sold on 13 Nov 2026".</summary>
    public static string SoldReason(DateOnly saleDate) =>
        $"Animal sold on {saleDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";

    /// <summary>"Animal died on 13 Nov 2026".</summary>
    public static string DiedReason(DateOnly deathDate) =>
        $"Animal died on {deathDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";

    public async Task<AnimalExitResult> ProcessExitAsync(Guid animalId, DateOnly exitDate, string reason, CancellationToken cancellationToken = default)
    {
        var animal = await _animals.GetByIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null)
            return AnimalExitResult.None;

        var tenantId = animal.TenantId;
        var farmId = animal.FarmId;
        var touched = new HashSet<Guid> { animalId };
        int plansCancelled = 0, skipped = 0, reversed = 0, resplit = 0;

        // 1. The animal's own plans end on the exit day.
        foreach (var plan in await _plans.GetAllPlansForAnimalAsync(tenantId, animalId, cancellationToken).ConfigureAwait(false))
        {
            if (plan.EndOnExit(exitDate))
            {
                _plans.Update(plan);
                plansCancelled++;
            }
        }

        // 2-3. Entries of its individual plans.
        var ownEntries = await _entries.GetEntriesByAnimalIdAsync(tenantId, animalId, cancellationToken).ConfigureAwait(false);
        foreach (var entry in ownEntries.Where(e => e.EntryDate >= exitDate))
        {
            if (entry.Status == DailyFeedingEntryStatus.Pending)
            {
                // A plan, not a fact: dropped the same way the Skip command drops it.
                var rows = await FeedingEntrySkip.SkipAsync(entry, reason, _entries, _allocations, cancellationToken).ConfigureAwait(false);
                touched.UnionWith(rows.Select(r => r.AnimalId));
                skipped++;
            }
            else if (entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted
                     && entry.EntryDate > exitDate)
            {
                // A fact that cannot be true: the animal was gone. The exit day itself stays.
                await ReverseConfirmedAsync(entry, reason, touched, cancellationToken).ConfigureAwait(false);
                reversed++;
            }
        }

        // 4. Group entries (batch / shed / pen) it still holds a share of after leaving.
        var groupRows = await _allocations.GetByAnimalAsync(animalId, exitDate, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);
        var groupEntryIds = groupRows
            .Where(r => r.Origin == FeedAllocationOrigin.GroupPlanShare)
            .Select(r => r.DailyFeedingEntryId)
            .Distinct()
            .ToList();

        foreach (var entryId in groupEntryIds)
        {
            var outcome = await ResplitGroupEntryAsync(entryId, animalId, exitDate, reason, touched, cancellationToken).ConfigureAwait(false);
            if (outcome == GroupOutcome.Resplit) resplit++;
            else if (outcome == GroupOutcome.Skipped) skipped++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 5. Ledger feed buckets are set (not added to) from confirmed allocations, so the exited
        // animal and every animal whose share moved read right however often this runs.
        await AnimalFeedCostSync.SyncAsync(_allocations, _costLedgers, tenantId, farmId, touched, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var result = new AnimalExitResult(plansCancelled, skipped, reversed, resplit);
        if (result.ChangedAnything && _logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Exit of animal {AnimalId} on {ExitDate}: {Plans} plan(s) cancelled, {Skipped} entr(ies) skipped, {Reversed} reversed, {Resplit} group entr(ies) re-split.",
                animalId, exitDate, plansCancelled, skipped, reversed, resplit);
        }

        return result;
    }

    private enum GroupOutcome { Unchanged, Resplit, Skipped }

    private async Task<GroupOutcome> ResplitGroupEntryAsync(
        Guid entryId, Guid animalId, DateOnly exitDate, string reason, HashSet<Guid> touched, CancellationToken cancellationToken)
    {
        var entry = await _entries.GetByIdAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
            return GroupOutcome.Unchanged;

        var confirmed = entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted;
        var pendingDue = entry.Status == DailyFeedingEntryStatus.Pending && entry.EntryDate >= exitDate;
        var confirmedDue = confirmed && entry.EntryDate > exitDate;
        if (!pendingDue && !confirmedDue)
            return GroupOutcome.Unchanged;

        var plan = await _plans.GetByIdAsync(entry.FeedingPlanId, cancellationToken).ConfigureAwait(false);
        if (plan is null || plan.AnimalId.HasValue)
            return GroupOutcome.Unchanged;

        var existing = await _allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        if (!existing.Any(r => r.AnimalId == animalId))
            return GroupOutcome.Unchanged; // already re-split by an earlier run

        var rebuilt = await _allocationService.RebuildGroupSharesAsync(entry, plan, existing, cancellationToken).ConfigureAwait(false);
        if (rebuilt.Shares.Count == 0)
        {
            if (pendingDue)
            {
                // Nobody left in the group that day: nothing to feed.
                var rows = await FeedingEntrySkip.SkipAsync(entry, $"{reason}; no animals left in this group", _entries, _allocations, cancellationToken).ConfigureAwait(false);
                touched.UnionWith(rows.Select(r => r.AnimalId));
                return GroupOutcome.Skipped;
            }

            // Confirmed feed given to a pen that, it turns out, was empty: the cost stays where it
            // is rather than vanishing; someone has to look at it.
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Confirmed group feeding entry {EntryId} on {Date} has no animal left to carry its cost after animal {AnimalId} left; allocation unchanged.",
                    entry.Id, entry.EntryDate, animalId);
            }
            return GroupOutcome.Unchanged;
        }

        var byAnimal = existing.ToDictionary(r => r.AnimalId);
        var userId = _currentUser.UserId ?? Guid.Empty;
        var added = new List<AnimalFeedAllocation>();
        foreach (var share in rebuilt.Shares)
        {
            if (byAnimal.Remove(share.AnimalId, out var row))
            {
                row.Reallocate(share.AllocatedKg, share.AllocatedCostBdt, rebuilt.UnitCostBdtPerKg,
                    share.WeightKg, share.ShareFactor, rebuilt.HeadCount, rebuilt.Method);
            }
            else
            {
                added.Add(AnimalFeedAllocation.Create(
                    tenantId: entry.TenantId,
                    animalId: share.AnimalId,
                    farmId: entry.FarmId,
                    dailyFeedingEntryId: entry.Id,
                    feedingPlanId: plan.Id,
                    formulaId: entry.FormulaId,
                    ruleLineId: entry.RuleLineId,
                    entryDate: entry.EntryDate,
                    allocatedKg: share.AllocatedKg,
                    allocatedCostBdt: share.AllocatedCostBdt,
                    unitCostBdtPerKg: rebuilt.UnitCostBdtPerKg,
                    origin: FeedAllocationOrigin.GroupPlanShare,
                    method: rebuilt.Method,
                    weightAtAllocationKg: share.WeightKg,
                    shareFactor: share.ShareFactor,
                    headCountAtAllocation: rebuilt.HeadCount,
                    batchId: entry.BatchId ?? plan.BatchId,
                    shedId: entry.ShedId ?? plan.ShedId,
                    penId: entry.PenId ?? plan.PenId));
            }
        }

        // Whoever is left over was not on the farm that day (the exited animal): its share goes.
        foreach (var gone in byAnimal.Values)
            gone.SoftDelete(userId);

        _allocations.UpdateRange(existing);
        if (added.Count > 0)
            _allocations.AddRange(added);

        touched.UnionWith(existing.Select(r => r.AnimalId));
        touched.UnionWith(added.Select(r => r.AnimalId));

        if (pendingDue)
        {
            // The planned quantity and cost cover the heads actually there.
            var basis = _rules.For(entry.TenantId, entry.FarmId).GroupPlanQuantityBasis;
            entry.SetConsumptionCost(rebuilt.UnitCostBdtPerKg, GroupFeedQuantity.HeadMultiplier(true, rebuilt.HeadCount, basis));
            _entries.Update(entry);
        }

        return GroupOutcome.Resplit;
    }

    /// <summary>
    /// Reverses a confirmed individual entry dated after the exit: stock back at the cost it was
    /// issued at, the feed-cost ledger postings removed, the entry voided, its rows zeroed.
    /// </summary>
    private async Task ReverseConfirmedAsync(DailyFeedingEntry entry, string reason, HashSet<Guid> touched, CancellationToken cancellationToken)
    {
        var movements = await _stockTransactions.GetByFarmIdAsync(entry.FarmId, entry.EntryDate, null, cancellationToken).ConfigureAwait(false);
        var mine = movements.Where(t => t.ReferenceId == entry.Id).ToList();
        var alreadyReturned = mine
            .Where(t => t.TransactionType == StockTransactionType.FeedConsumptionReversal)
            .Select(t => t.InventoryItemId)
            .ToHashSet();
        var today = _clock.Today;

        foreach (var issued in mine.Where(t => t.TransactionType == StockTransactionType.PlannedFeedConsumption))
        {
            if (!alreadyReturned.Add(issued.InventoryItemId))
                continue;

            var item = await _inventory.GetByIdAsync(issued.InventoryItemId, cancellationToken).ConfigureAwait(false);
            if (item is null)
                continue;

            item.ReturnConsumedStock(issued.Quantity, issued.UnitCostBdt);
            _inventory.Update(item);

            await _stockTransactions.AddAsync(new StockTransaction(
                id: Guid.NewGuid(),
                tenantId: entry.TenantId,
                farmId: entry.FarmId,
                inventoryItemId: item.Id,
                transactionType: StockTransactionType.FeedConsumptionReversal,
                quantity: issued.Quantity,
                unitCostBdt: issued.UnitCostBdt,
                balanceAfter: item.CurrentStock,
                transactionDate: today < entry.EntryDate ? entry.EntryDate : today,
                reason: $"{reason}: feed for {entry.EntryDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)} returned to stock (entry {entry.Id})",
                referenceId: entry.Id), cancellationToken).ConfigureAwait(false);
        }

        // The feed handler books two postings per entry: feed from stock (keyed on the entry) and
        // ingredients not tracked in stock (keyed on its "direct-feed" sub key). Both go.
        await _ledgerPosting.ReverseAsync(entry.FarmId, entry.Id, TransactionCategory.FeedCost, cancellationToken).ConfigureAwait(false);
        await _ledgerPosting.ReverseAsync(entry.FarmId, LedgerPosting.SubKey(entry.Id, "direct-feed"), TransactionCategory.FeedCost, cancellationToken).ConfigureAwait(false);

        entry.VoidAfterExit(reason);
        _entries.Update(entry);

        var rows = await _allocations.GetByEntryIdAcrossTenantsAsync(entry.Id, cancellationToken).ConfigureAwait(false);
        if (rows.Count > 0)
        {
            foreach (var row in rows)
                row.UpdateConsumption(0m, 0m, row.UnitCostBdtPerKg);
            _allocations.UpdateRange(rows);
            touched.UnionWith(rows.Select(r => r.AnimalId));
        }
    }
}
