using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Feeding.Services;

public interface IFeedAllocationService
{
    /// <summary>
    /// Produces one <see cref="AnimalFeedAllocation"/> per animal covered by a feeding entry.
    /// Returns empty when the plan's scope currently holds no animals.
    /// </summary>
    Task<IReadOnlyList<AnimalFeedAllocation>> BuildAllocationsAsync(
        DailyFeedingEntry entry,
        AnimalFeedingPlan plan,
        bool isBackfill = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-splits a group entry over the animals actually fed on its date: the scope's members by
    /// the "fed on day D" rule, plus any animal already holding a row that was still on the farm
    /// that day. A pending entry is re-planned (per-head ration x the new head count); a confirmed
    /// entry keeps its total feed and cost (it was given to the pen) and only the split changes.
    /// Returns no shares when nobody was in the group that day.
    /// </summary>
    Task<GroupReallocation> RebuildGroupSharesAsync(
        DailyFeedingEntry entry,
        AnimalFeedingPlan plan,
        IReadOnlyList<AnimalFeedAllocation> existingRows,
        CancellationToken cancellationToken = default);
}

/// <summary>The new split of a group entry (see <see cref="IFeedAllocationService.RebuildGroupSharesAsync"/>).</summary>
public sealed record GroupReallocation(
    IReadOnlyList<FeedAllocationShare> Shares,
    int HeadCount,
    decimal UnitCostBdtPerKg,
    FeedAllocationMethod Method)
{
    public static GroupReallocation Empty(decimal unitCost, FeedAllocationMethod method) => new([], 0, unitCost, method);
}

/// <summary>
/// Turns a plan-level feeding entry into per-animal feed rows (docs/32 GAP-1).
/// </summary>
/// <remarks>
/// <para>
/// Two cases. An <b>individual plan</b> names its animal, so the entry belongs to it whole.
/// A <b>group plan</b> (batch, shed or pen) names none, and its quantity has to be spread over
/// whichever animals were in that scope on the day.
/// </para>
/// <para>
/// <b>The quantity is a per-head ration, not a group total.</b> Feeding rule lines are matched on
/// a per-animal weight band ("200-250 kg → 3 kg/day"), and the WeightPercentage plan type computes
/// <c>bodyWeight × %</c> — both describe one animal. A batch plan's entry therefore records one
/// animal's ration, and the group's real consumption is that ration times the head count. Farms
/// that instead record group totals can say so via
/// <see cref="FarmBusinessRules.GroupPlanQuantityBasis"/>.
/// </para>
/// </remarks>
public sealed class FeedAllocationService : IFeedAllocationService
{
    private readonly IAnimalRepository _animalRepository;
    private readonly IFarmBusinessRulesProvider _rules;

    public FeedAllocationService(IAnimalRepository animalRepository, IFarmBusinessRulesProvider rules)
    {
        _animalRepository = animalRepository;
        _rules = rules;
    }

    public async Task<IReadOnlyList<AnimalFeedAllocation>> BuildAllocationsAsync(
        DailyFeedingEntry entry,
        AnimalFeedingPlan plan,
        bool isBackfill = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(plan);

        // The entry's own tenant and farm: this also runs from jobs that have no request tenant.
        var rules = _rules.For(entry.TenantId, entry.FarmId);

        var (animals, origin) = await ResolveSubjectsAsync(plan, entry.EntryDate, cancellationToken).ConfigureAwait(false);
        if (animals.Count == 0)
        {
            // A plan whose scope is empty today (batch sold off, pen cleared) allocates nothing.
            // Returning empty is correct; inventing a phantom animal to absorb the cost is not.
            return [];
        }

        // Actual consumption where it was recorded; the plan's expectation otherwise.
        var perHeadKg = entry.ActualKg ?? entry.ExpectedKg;
        var unitCost = entry.UnitCostAtConsumptionBdt ?? 0m;

        var totalKg = rules.GroupPlanQuantityBasis == GroupPlanQuantityBasis.PerHead
            ? perHeadKg * animals.Count
            : perHeadKg;

        var totalCost = decimal.Round(totalKg * unitCost, FeedAllocationCalculator.MoneyDecimals, MidpointRounding.AwayFromZero);

        var subjects = animals
            .Select(a => new FeedAllocationSubject(a.Id, a.LatestWeightKg ?? 0m))
            .ToList();

        var shares = FeedAllocationCalculator.Allocate(
            subjects,
            totalKg,
            totalCost,
            origin == FeedAllocationOrigin.IndividualPlan ? FeedAllocationMethod.EqualPerHead : rules.FeedAllocation,
            rules.MetabolicWeightExponent,
            rules.FallbackAnimalWeightKg);

        var allocations = new List<AnimalFeedAllocation>(shares.Count);

        foreach (var share in shares)
        {
            allocations.Add(AnimalFeedAllocation.Create(
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
                unitCostBdtPerKg: unitCost,
                origin: origin,
                method: origin == FeedAllocationOrigin.IndividualPlan
                    ? FeedAllocationMethod.EqualPerHead
                    : rules.FeedAllocation,
                weightAtAllocationKg: share.WeightKg,
                shareFactor: share.ShareFactor,
                headCountAtAllocation: animals.Count,
                batchId: entry.BatchId ?? plan.BatchId,
                shedId: entry.ShedId ?? plan.ShedId,
                penId: entry.PenId ?? plan.PenId,
                isBackfilled: isBackfill));
        }

        return allocations;
    }

    public async Task<GroupReallocation> RebuildGroupSharesAsync(
        DailyFeedingEntry entry,
        AnimalFeedingPlan plan,
        IReadOnlyList<AnimalFeedAllocation> existingRows,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(existingRows);

        // The entry's own tenant and farm: this also runs from jobs that have no request tenant.
        var rules = _rules.For(entry.TenantId, entry.FarmId);
        var method = rules.FeedAllocation;
        var confirmed = entry.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted;
        var unitCost = confirmed
            ? existingRows.Select(r => r.UnitCostBdtPerKg).FirstOrDefault(c => c > 0, entry.UnitCostAtConsumptionBdt ?? 0m)
            : entry.UnitCostAtConsumptionBdt ?? 0m;

        var (scopeMembers, _) = await ResolveSubjectsAsync(plan, entry.EntryDate, cancellationToken).ConfigureAwait(false);
        var members = scopeMembers.ToDictionary(a => a.Id);

        // An animal already sharing this entry stays in it while it was on the farm that day, even
        // if the scope lookup no longer places it there: only leaving the farm takes a share away.
        foreach (var animalId in existingRows.Select(r => r.AnimalId).Distinct().Where(id => !members.ContainsKey(id)))
        {
            var animal = await _animalRepository.GetByIdAcrossTenantsAsync(animalId, cancellationToken).ConfigureAwait(false);
            if (animal is not null && AnimalFeedingPresence.IsFedOn(animal, entry.EntryDate))
                members[animal.Id] = animal;
        }

        if (members.Count == 0)
            return GroupReallocation.Empty(unitCost, method);

        decimal totalKg;
        decimal totalCost;
        if (confirmed)
        {
            // Given to the pen and taken from stock: the totals are facts, only the split changes.
            totalKg = existingRows.Sum(r => r.AllocatedKg);
            totalCost = existingRows.Sum(r => r.AllocatedCostBdt);
        }
        else
        {
            var perHeadKg = entry.ActualKg ?? entry.ExpectedKg;
            totalKg = rules.GroupPlanQuantityBasis == GroupPlanQuantityBasis.PerHead ? perHeadKg * members.Count : perHeadKg;
            totalCost = decimal.Round(totalKg * unitCost, FeedAllocationCalculator.MoneyDecimals, MidpointRounding.AwayFromZero);
        }

        var subjects = members.Values
            .OrderBy(a => a.Id)
            .Select(a => new FeedAllocationSubject(a.Id, a.LatestWeightKg ?? 0m))
            .ToList();

        var shares = FeedAllocationCalculator.Allocate(
            subjects, totalKg, totalCost, method, rules.MetabolicWeightExponent, rules.FallbackAnimalWeightKg);

        return new GroupReallocation(shares, members.Count, unitCost, method);
    }

    private async Task<(IReadOnlyList<Animal> Animals, FeedAllocationOrigin Origin)> ResolveSubjectsAsync(
        AnimalFeedingPlan plan,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        if (plan.AnimalId is { } animalId)
        {
            var animal = await _animalRepository
                .GetByIdAcrossTenantsAsync(animalId, cancellationToken)
                .ConfigureAwait(false);

            // Fed on the day only if on the farm that day (AnimalFeedingPresence): a sold or dead
            // animal takes no share of an entry dated on or after its exit.
            return animal is null || !AnimalFeedingPresence.IsFedOn(animal, asOf)
                ? ([], FeedAllocationOrigin.IndividualPlan)
                : ([animal], FeedAllocationOrigin.IndividualPlan);
        }

        var members = await _animalRepository
            .GetActiveByScopeAcrossTenantsAsync(plan.TenantId, plan.BatchId, plan.ShedId, plan.PenId, asOf, cancellationToken)
            .ConfigureAwait(false);

        return (members, FeedAllocationOrigin.GroupPlanShare);
    }
}
