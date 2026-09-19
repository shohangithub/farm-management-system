using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
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

        var rules = _rules.Current;

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

            return animal is null
                ? ([], FeedAllocationOrigin.IndividualPlan)
                : ([animal], FeedAllocationOrigin.IndividualPlan);
        }

        var members = await _animalRepository
            .GetActiveByScopeAcrossTenantsAsync(plan.TenantId, plan.BatchId, plan.ShedId, plan.PenId, asOf, cancellationToken)
            .ConfigureAwait(false);

        return (members, FeedAllocationOrigin.GroupPlanShare);
    }
}
