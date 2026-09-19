using System;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding;

/// <summary>Where an allocation row's numbers came from.</summary>
public enum FeedAllocationOrigin
{
    /// <summary>The plan named this animal directly; no division was needed.</summary>
    IndividualPlan = 0,

    /// <summary>The animal shared a batch, shed or pen plan and received a computed share.</summary>
    GroupPlanShare = 1,
}

/// <summary>
/// One animal's share of one day's feed, resolved from a feeding plan and persisted.
/// </summary>
/// <remarks>
/// <para>
/// Closes GAP-1 in docs/32. <see cref="DailyFeedingEntry"/> hangs off a feeding plan, and a plan's
/// <c>AnimalId</c> is nullable because it may instead cover a batch, shed or pen. Joining an animal
/// to its entries therefore works only for animals on individual plans; for group-fed animals the
/// join returns nothing, and their feed cost silently reads as zero. This table is the missing
/// per-animal grain.
/// </para>
/// <para>
/// <b>Computed once and stored, never derived at read time.</b> Herd composition, animal weights
/// and the weighted-average feed cost all move. Recomputing on read would mean a report re-run
/// next year quietly disagreeing with the copy already signed and filed. Storing the result —
/// together with the method, exponent and head count that produced it — is what makes a printed
/// report defensible.
/// </para>
/// </remarks>
public sealed class AnimalFeedAllocation : AuditableEntity
{
    private AnimalFeedAllocation() { } // EF Core

    public Guid AnimalId { get; private set; }

    public Guid FarmId { get; private set; }

    /// <summary>The group feed record this share came out of.</summary>
    public Guid DailyFeedingEntryId { get; private set; }

    public Guid FeedingPlanId { get; private set; }

    public Guid FormulaId { get; private set; }

    public Guid? RuleLineId { get; private set; }

    public Guid? BatchId { get; private set; }

    public Guid? ShedId { get; private set; }

    public Guid? PenId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public decimal AllocatedKg { get; private set; }

    public decimal AllocatedCostBdt { get; private set; }

    public decimal UnitCostBdtPerKg { get; private set; }

    public FeedAllocationOrigin Origin { get; private set; }

    public FeedAllocationMethod Method { get; private set; }

    /// <summary>Weight used for this animal's share — recorded because weights change.</summary>
    public decimal WeightAtAllocationKg { get; private set; }

    /// <summary>This animal's fraction of the group. Stored so a share can be audited without re-deriving it.</summary>
    public decimal ShareFactor { get; private set; }

    /// <summary>Animals in the group on <see cref="EntryDate"/>. 1 for an individual plan.</summary>
    public int HeadCountAtAllocation { get; private set; }

    /// <summary>
    /// True when produced by the historical backfill rather than by the daily job.
    /// </summary>
    /// <remarks>
    /// Backfill reconstructs group membership from today's herd, because Farm360 does not keep a
    /// dated membership history. For past periods where animals moved between pens the split is an
    /// approximation, and an auditor is entitled to know which rows those are.
    /// </remarks>
    public bool IsBackfilled { get; private set; }

    public static AnimalFeedAllocation Create(
        Guid tenantId,
        Guid animalId,
        Guid farmId,
        Guid dailyFeedingEntryId,
        Guid feedingPlanId,
        Guid formulaId,
        Guid? ruleLineId,
        DateOnly entryDate,
        decimal allocatedKg,
        decimal allocatedCostBdt,
        decimal unitCostBdtPerKg,
        FeedAllocationOrigin origin,
        FeedAllocationMethod method,
        decimal weightAtAllocationKg,
        decimal shareFactor,
        int headCountAtAllocation,
        Guid? batchId = null,
        Guid? shedId = null,
        Guid? penId = null,
        bool isBackfilled = false)
    {
        if (animalId == Guid.Empty)
        {
            throw new ArgumentException("AnimalId cannot be empty.", nameof(animalId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(allocatedKg);
        ArgumentOutOfRangeException.ThrowIfLessThan(headCountAtAllocation, 1);

        var allocation = new AnimalFeedAllocation
        {
            Id = Guid.NewGuid(),
            AnimalId = animalId,
            FarmId = farmId,
            DailyFeedingEntryId = dailyFeedingEntryId,
            FeedingPlanId = feedingPlanId,
            FormulaId = formulaId,
            RuleLineId = ruleLineId,
            EntryDate = entryDate,
            AllocatedKg = allocatedKg,
            AllocatedCostBdt = allocatedCostBdt,
            UnitCostBdtPerKg = unitCostBdtPerKg,
            Origin = origin,
            Method = method,
            WeightAtAllocationKg = weightAtAllocationKg,
            ShareFactor = shareFactor,
            HeadCountAtAllocation = headCountAtAllocation,
            BatchId = batchId,
            ShedId = shedId,
            PenId = penId,
            IsBackfilled = isBackfilled,
        };

        allocation.SetTenantId(tenantId);
        return allocation;
    }
}
