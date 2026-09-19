using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Allocation;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.Services;

public sealed record OverheadAllocationResult(
    int TransactionsConsidered,
    int TransactionsAllocated,
    int TransactionsSkippedAlreadyAllocated,
    int TransactionsSkippedNoAnimals,
    int AllocationsWritten,
    decimal AmountAllocatedBdt,
    int LedgersUpdated);

public interface IOverheadAllocationService
{
    /// <summary>
    /// Allocates a farm's unattributed indirect costs in a period onto its animals and
    /// recomputes the affected cost ledgers. Safe to run repeatedly over the same period.
    /// </summary>
    Task<OverheadAllocationResult> AllocateAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        bool isBackfill = false,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pushes farm-level labour and overhead down onto individual animals (docs/32 GAP-2).
/// </summary>
/// <remarks>
/// <para>
/// Without this, <see cref="AnimalCostLedger"/>'s labour and overhead buckets stay at zero and
/// its "total cost" is a direct cost wearing a full-absorption label. Every margin, break-even
/// and performance ranking built on it flatters the animal.
/// </para>
/// <para>
/// <b>Idempotent by construction, twice over.</b> Transactions already allocated are skipped, and
/// the ledger buckets are then <i>recomputed</i> from the allocation table rather than incremented.
/// A second run over the same period therefore writes nothing and changes nothing, which is what
/// makes it safe to schedule monthly, resume after a failure, or re-run a corrected month.
/// </para>
/// </remarks>
public sealed class OverheadAllocationService : IOverheadAllocationService
{
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IAnimalOverheadAllocationRepository _allocations;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalRepository _animals;
    private readonly IFarmBusinessRulesProvider _rules;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OverheadAllocationService> _logger;

    public OverheadAllocationService(
        IFinancialTransactionRepository transactions,
        IAnimalOverheadAllocationRepository allocations,
        IAnimalCostLedgerRepository ledgers,
        IAnimalRepository animals,
        IFarmBusinessRulesProvider rules,
        IUnitOfWork unitOfWork,
        ILogger<OverheadAllocationService> logger)
    {
        _transactions = transactions;
        _allocations = allocations;
        _ledgers = ledgers;
        _animals = animals;
        _rules = rules;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<OverheadAllocationResult> AllocateAsync(
        Guid farmId,
        DateOnly from,
        DateOnly to,
        bool isBackfill = false,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ArgumentException("The period end cannot precede its start.", nameof(to));
        }

        var rules = _rules.Current;

        var candidates = await _transactions
            .GetUnattributedIndirectCostsAsync(farmId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var alreadyAllocated = await _allocations
            .GetAllocatedTransactionIdsAsync(farmId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var herd = await _animals
            .GetPresentDuringPeriodAsync(farmId, from, to, cancellationToken)
            .ConfigureAwait(false);

        var considered = candidates.Count;
        var allocatedCount = 0;
        var skippedAllocated = 0;
        var skippedNoAnimals = 0;
        var rowsWritten = 0;
        var amountAllocated = 0m;
        var touchedAnimals = new HashSet<Guid>();

        foreach (var transaction in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (alreadyAllocated.Contains(transaction.Id))
            {
                skippedAllocated++;
                continue;
            }

            var bucket = AnimalOverheadAllocation.BucketFor(transaction.Category);
            if (bucket is null)
            {
                // A direct cost that the repository query let through. Allocating it would
                // double-count against the module that already attributed it.
                continue;
            }

            var (periodStart, periodEnd) = CostPeriodFor(transaction, rules.OverheadCostPeriod);

            var subjects = BuildSubjects(herd, periodStart, periodEnd);
            if (subjects.Count == 0)
            {
                // The expense exists but no animal was on the farm to incur it. Leaving it
                // unallocated is correct; spreading it over animals that were not there is not.
                skippedNoAnimals++;
                continue;
            }

            var shares = OverheadAllocationCalculator.Allocate(
                subjects,
                transaction.AmountBdt,
                rules.OverheadAllocation,
                rules.FallbackAnimalWeightKg);

            var rows = new List<AnimalOverheadAllocation>(shares.Count);

            foreach (var share in shares)
            {
                // A zero share means the animal was not present for any of the period; writing a
                // zero row would only add noise to the audit trail.
                if (share.AllocatedAmountBdt == 0m && share.HeadDays == 0m)
                {
                    continue;
                }

                rows.Add(AnimalOverheadAllocation.Create(
                    tenantId: transaction.TenantId,
                    animalId: share.AnimalId,
                    farmId: farmId,
                    sourceTransactionId: transaction.Id,
                    category: transaction.Category,
                    bucket: bucket.Value,
                    periodStart: periodStart,
                    periodEnd: periodEnd,
                    allocatedAmountBdt: share.AllocatedAmountBdt,
                    method: rules.OverheadAllocation,
                    headDays: share.HeadDays,
                    weightAtAllocationKg: share.WeightKg,
                    shareFactor: share.ShareFactor,
                    headCountAtAllocation: subjects.Count,
                    isBackfilled: isBackfill));

                touchedAnimals.Add(share.AnimalId);
            }

            if (rows.Count == 0)
            {
                skippedNoAnimals++;
                continue;
            }

            _allocations.AddRange(rows);
            allocatedCount++;
            rowsWritten += rows.Count;
            amountAllocated += transaction.AmountBdt;
        }

        // Persist the allocations before recomputing, so the totals query sees them.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var ledgersUpdated = await RefreshLedgersAsync(touchedAnimals, cancellationToken).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Overhead allocation for farm {FarmId} {From}..{To}: {Allocated}/{Considered} transactions, " +
                "{Rows} rows, {Amount} BDT, {Ledgers} ledgers updated ({SkippedDone} already allocated, {SkippedNone} with no animals).",
                farmId, from, to, allocatedCount, considered, rowsWritten, amountAllocated,
                ledgersUpdated, skippedAllocated, skippedNoAnimals);
        }

        return new OverheadAllocationResult(
            considered, allocatedCount, skippedAllocated, skippedNoAnimals,
            rowsWritten, amountAllocated, ledgersUpdated);
    }

    /// <summary>
    /// Recomputes the labour and overhead buckets of every affected ledger from the allocation
    /// table. Setting rather than adding is what makes a re-run harmless.
    /// </summary>
    private async Task<int> RefreshLedgersAsync(HashSet<Guid> animalIds, CancellationToken cancellationToken)
    {
        if (animalIds.Count == 0)
        {
            return 0;
        }

        var totals = await _allocations
            .GetTotalsForAnimalsAsync(animalIds, cancellationToken)
            .ConfigureAwait(false);

        var updated = 0;

        foreach (var total in totals)
        {
            var ledger = await _ledgers
                .GetByAnimalIdAsync(total.AnimalId, cancellationToken)
                .ConfigureAwait(false);

            if (ledger is null)
            {
                // No ledger yet: the animal's costing record is created by its own module on
                // first use. Skipping is right — inventing one here would set the acquisition
                // cost to zero and quietly corrupt its margin.
                continue;
            }

            ledger.UpdateLaborCost(total.LabourBdt);
            ledger.UpdateOverheadCost(total.OverheadBdt);
            _ledgers.Update(ledger);
            updated++;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return updated;
    }

    private static (DateOnly Start, DateOnly End) CostPeriodFor(FinancialTransaction transaction, OverheadCostPeriod period)
    {
        var date = DateOnly.FromDateTime(transaction.TransactionDate);

        if (period == OverheadCostPeriod.TransactionDate)
        {
            return (date, date);
        }

        var start = new DateOnly(date.Year, date.Month, 1);
        var end = new DateOnly(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));
        return (start, end);
    }

    private static List<OverheadAllocationSubject> BuildSubjects(
        IReadOnlyList<Animal> herd,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        var subjects = new List<OverheadAllocationSubject>(herd.Count);

        foreach (var animal in herd)
        {
            var headDays = OverheadAllocationCalculator.HeadDays(
                periodStart, periodEnd, animal.AcquisitionDate, animal.SaleDate);

            if (headDays <= 0m)
            {
                continue;
            }

            subjects.Add(new OverheadAllocationSubject(animal.Id, headDays, animal.LatestWeightKg ?? 0m));
        }

        return subjects;
    }

    /// <summary>Formats a period for log and report display.</summary>
    public static string DescribePeriod(DateOnly from, DateOnly to) =>
        $"{from.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)} to {to.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)}";
}
