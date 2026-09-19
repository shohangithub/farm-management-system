using System;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Common;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance;

/// <summary>Which ledger bucket an allocated indirect cost lands in.</summary>
public enum OverheadBucket
{
    /// <summary>Wages and salaries — <see cref="AnimalCostLedger.TotalLaborCostBdt"/>.</summary>
    Labour = 0,

    /// <summary>Utilities, transport, consumables, sundries — <see cref="AnimalCostLedger.TotalOverheadBdt"/>.</summary>
    Overhead = 1,
}

/// <summary>
/// One animal's share of one farm-level indirect cost (docs/32 GAP-2).
/// </summary>
/// <remarks>
/// <para>
/// Labour, utilities, transport and sundries are booked against the farm, not an animal, so
/// <see cref="AnimalCostLedger"/> carried empty labour and overhead buckets and its "total cost"
/// was really a direct cost. Any margin or ranking built on it understated what the animal
/// actually cost to keep. This table is the missing link between a farm expense and the animals
/// that incurred it.
/// </para>
/// <para>
/// One row per (source transaction, animal). The pair is uniquely indexed, so re-running the
/// allocation cannot post the same expense twice — and because the ledger buckets are recomputed
/// from these rows rather than incremented, a re-run is a no-op rather than a doubling.
/// </para>
/// </remarks>
public sealed class AnimalOverheadAllocation : AuditableEntity
{
    private AnimalOverheadAllocation() { } // EF Core

    public Guid AnimalId { get; private set; }

    public Guid FarmId { get; private set; }

    /// <summary>The farm-level expense this share came out of.</summary>
    public Guid SourceTransactionId { get; private set; }

    public TransactionCategory Category { get; private set; }

    public OverheadBucket Bucket { get; private set; }

    /// <summary>Period the source expense covers — a monthly wage bill is not a single-day cost.</summary>
    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public decimal AllocatedAmountBdt { get; private set; }

    public OverheadAllocationMethod Method { get; private set; }

    /// <summary>Days this animal was on the farm within the period — the weighting factor.</summary>
    public decimal HeadDays { get; private set; }

    /// <summary>Weight used when the method is <see cref="OverheadAllocationMethod.PerLiveWeight"/>.</summary>
    public decimal WeightAtAllocationKg { get; private set; }

    /// <summary>This animal's fraction of the expense. Stored so a share can be audited directly.</summary>
    public decimal ShareFactor { get; private set; }

    /// <summary>Animals sharing the expense. Recorded because the herd changes.</summary>
    public int HeadCountAtAllocation { get; private set; }

    public bool IsBackfilled { get; private set; }

    public static AnimalOverheadAllocation Create(
        Guid tenantId,
        Guid animalId,
        Guid farmId,
        Guid sourceTransactionId,
        TransactionCategory category,
        OverheadBucket bucket,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocatedAmountBdt,
        OverheadAllocationMethod method,
        decimal headDays,
        decimal weightAtAllocationKg,
        decimal shareFactor,
        int headCountAtAllocation,
        bool isBackfilled = false)
    {
        if (animalId == Guid.Empty)
        {
            throw new ArgumentException("AnimalId cannot be empty.", nameof(animalId));
        }

        if (sourceTransactionId == Guid.Empty)
        {
            throw new ArgumentException("SourceTransactionId cannot be empty.", nameof(sourceTransactionId));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(headCountAtAllocation, 1);

        var allocation = new AnimalOverheadAllocation
        {
            Id = Guid.NewGuid(),
            AnimalId = animalId,
            FarmId = farmId,
            SourceTransactionId = sourceTransactionId,
            Category = category,
            Bucket = bucket,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AllocatedAmountBdt = allocatedAmountBdt,
            Method = method,
            HeadDays = headDays,
            WeightAtAllocationKg = weightAtAllocationKg,
            ShareFactor = shareFactor,
            HeadCountAtAllocation = headCountAtAllocation,
            IsBackfilled = isBackfilled,
        };

        allocation.SetTenantId(tenantId);
        return allocation;
    }

    /// <summary>
    /// Which ledger bucket a category belongs to.
    /// </summary>
    /// <remarks>
    /// Only genuinely indirect categories are allocatable. Feed, veterinary, medicine and animal
    /// purchase are already attributed to an animal by their own modules; allocating them here
    /// would double-count them against what the feed allocation and treatment records already say.
    /// </remarks>
    public static OverheadBucket? BucketFor(TransactionCategory category) => category switch
    {
        TransactionCategory.LaborCost => OverheadBucket.Labour,
        TransactionCategory.Utilities
            or TransactionCategory.Transport
            or TransactionCategory.MiscellaneousExpense
            or TransactionCategory.ConsumableExpense => OverheadBucket.Overhead,
        _ => null,
    };
}
