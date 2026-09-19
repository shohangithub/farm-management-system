using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Common;

namespace Farm360.Domain.Feeding.Allocation;

/// <summary>One animal sharing a group feed, with the weight its share is computed from.</summary>
public readonly record struct FeedAllocationSubject(Guid AnimalId, decimal WeightKg);

/// <summary>One animal's computed share of a group feed.</summary>
public readonly record struct FeedAllocationShare(
    Guid AnimalId,
    decimal WeightKg,
    decimal ShareFactor,
    decimal AllocatedKg,
    decimal AllocatedCostBdt);

/// <summary>
/// Splits a group feed quantity and its cost between the animals that shared it.
/// </summary>
/// <remarks>
/// <para>
/// Pure and deterministic: same inputs, same output, no clock, no database. Every per-animal feed
/// cost in Farm360 flows through here, so it is the one place where the split can be reasoned
/// about, tested exhaustively and explained to an auditor.
/// </para>
/// <para>
/// <b>Losslessness matters more than elegance.</b> Naive proportional rounding leaves a few paisa
/// unassigned on almost every group, and those fractions accumulate into a permanent gap between
/// the sum of animal costs and the herd's actual feed spend — exactly the kind of drift that
/// destroys trust in a report pack. The largest-remainder method below distributes the residual
/// so the shares always add back to the total, to the last paisa.
/// </para>
/// </remarks>
public static class FeedAllocationCalculator
{
    /// <summary>Kilogram precision. Three decimals is grams — finer than any farm scale.</summary>
    public const int KgDecimals = 3;

    /// <summary>Money precision: 1 paisa.</summary>
    public const int MoneyDecimals = 2;

    /// <summary>
    /// Divides <paramref name="totalKg"/> and <paramref name="totalCostBdt"/> across
    /// <paramref name="subjects"/>. The returned shares sum exactly to the totals.
    /// </summary>
    public static IReadOnlyList<FeedAllocationShare> Allocate(
        IReadOnlyList<FeedAllocationSubject> subjects,
        decimal totalKg,
        decimal totalCostBdt,
        FeedAllocationMethod method,
        double metabolicExponent,
        decimal fallbackWeightKg)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        if (subjects.Count == 0)
        {
            return [];
        }

        // Stable order first: the largest-remainder tie-break must not depend on the order rows
        // happened to come back from the database, or the same day could allocate differently
        // on a re-run.
        var ordered = subjects.OrderBy(s => s.AnimalId).ToArray();

        var factors = new decimal[ordered.Length];
        decimal factorTotal = 0m;

        for (var i = 0; i < ordered.Length; i++)
        {
            var weight = ordered[i].WeightKg > 0 ? ordered[i].WeightKg : fallbackWeightKg;
            if (weight <= 0)
            {
                weight = 1m;
            }

            factors[i] = method switch
            {
                FeedAllocationMethod.EqualPerHead => 1m,
                FeedAllocationMethod.LiveWeight => weight,
                FeedAllocationMethod.MetabolicWeight => MetabolicWeight(weight, metabolicExponent),
                _ => 1m,
            };

            factorTotal += factors[i];
        }

        // Degenerate input (all weights zero under a weight-based method): fall back to an equal
        // split rather than dividing by zero or silently dropping the cost.
        if (factorTotal <= 0m)
        {
            for (var i = 0; i < factors.Length; i++)
            {
                factors[i] = 1m;
            }

            factorTotal = factors.Length;
        }

        var kg = ProportionalSplit.Distribute(totalKg, (decimal[])factors.Clone(), KgDecimals);
        var cost = ProportionalSplit.Distribute(totalCostBdt, (decimal[])factors.Clone(), MoneyDecimals);

        var result = new FeedAllocationShare[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            result[i] = new FeedAllocationShare(
                ordered[i].AnimalId,
                ordered[i].WeightKg,
                Math.Round(factors[i] / factorTotal, 6, MidpointRounding.AwayFromZero),
                kg[i],
                cost[i]);
        }

        return result;
    }

    /// <summary>
    /// W^exponent, the standard metabolic-weight scaling in ruminant nutrition (exponent 0.75).
    /// </summary>
    /// <remarks>
    /// Computed in double because decimal has no Pow; the result is immediately rounded back into
    /// decimal, and it is only a relative share factor, so the precision loss cannot reach money —
    /// the totals themselves are divided in decimal by <see cref="ProportionalSplit"/>.
    /// </remarks>
    public static decimal MetabolicWeight(decimal weightKg, double exponent)
    {
        if (weightKg <= 0m)
        {
            return 0m;
        }

        var value = Math.Pow((double)weightKg, exponent);

        return double.IsFinite(value) && value > 0
            ? (decimal)Math.Round(value, 6)
            : weightKg;
    }

}
