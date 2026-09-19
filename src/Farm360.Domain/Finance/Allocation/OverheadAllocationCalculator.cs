using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Common;

namespace Farm360.Domain.Finance.Allocation;

/// <summary>An animal that was on the farm for part or all of the cost period.</summary>
public readonly record struct OverheadAllocationSubject(Guid AnimalId, decimal HeadDays, decimal WeightKg);

/// <summary>One animal's share of a farm-level indirect cost.</summary>
public readonly record struct OverheadAllocationShare(
    Guid AnimalId,
    decimal HeadDays,
    decimal WeightKg,
    decimal ShareFactor,
    decimal AllocatedAmountBdt);

/// <summary>
/// Splits a farm-level indirect cost across the animals that were present while it was incurred.
/// </summary>
/// <remarks>
/// <para>
/// Pure and deterministic, like its feed counterpart, and for the same reason: this decides real
/// money on every animal's ledger, so it must be testable in isolation and explainable to an
/// auditor without reference to a database.
/// </para>
/// <para>
/// <b>Head-days, not head count.</b> An animal sold on the 10th of a month did not consume a full
/// month of labour. Weighting by days present means a mid-period arrival or departure carries its
/// fair share and no more — the difference between a defensible cost and an arbitrary one.
/// </para>
/// </remarks>
public static class OverheadAllocationCalculator
{
    public const int MoneyDecimals = 2;

    /// <summary>
    /// Divides <paramref name="totalAmountBdt"/> across <paramref name="subjects"/>.
    /// The returned shares sum exactly to the total.
    /// </summary>
    public static IReadOnlyList<OverheadAllocationShare> Allocate(
        IReadOnlyList<OverheadAllocationSubject> subjects,
        decimal totalAmountBdt,
        OverheadAllocationMethod method,
        decimal fallbackWeightKg)
    {
        ArgumentNullException.ThrowIfNull(subjects);

        if (subjects.Count == 0)
        {
            return [];
        }

        // Stable order so the largest-remainder tie-break is reproducible across runs.
        var ordered = subjects.OrderBy(s => s.AnimalId).ToArray();

        var factors = new decimal[ordered.Length];
        decimal factorTotal = 0m;

        for (var i = 0; i < ordered.Length; i++)
        {
            // An animal with no days present contributes nothing — it was not there.
            var days = ordered[i].HeadDays > 0 ? ordered[i].HeadDays : 0m;

            if (method == OverheadAllocationMethod.PerLiveWeight)
            {
                var weight = ordered[i].WeightKg > 0 ? ordered[i].WeightKg : fallbackWeightKg;
                if (weight <= 0)
                {
                    weight = 1m;
                }

                // Weight-days: a heavy animal present half the period should not outrank a
                // lighter one present throughout purely on weight.
                factors[i] = days * weight;
            }
            else
            {
                factors[i] = days;
            }

            factorTotal += factors[i];
        }

        if (factorTotal <= 0m)
        {
            for (var i = 0; i < factors.Length; i++)
            {
                factors[i] = 1m;
            }

            factorTotal = factors.Length;
        }

        var amounts = ProportionalSplit.Distribute(totalAmountBdt, (decimal[])factors.Clone(), MoneyDecimals);

        var result = new OverheadAllocationShare[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            result[i] = new OverheadAllocationShare(
                ordered[i].AnimalId,
                ordered[i].HeadDays,
                ordered[i].WeightKg,
                Math.Round(factors[i] / factorTotal, 6, MidpointRounding.AwayFromZero),
                amounts[i]);
        }

        return result;
    }

    /// <summary>
    /// Days an animal was on the farm within a cost period: the overlap between its stay and the
    /// period, inclusive. Zero when the two do not overlap.
    /// </summary>
    public static decimal HeadDays(DateOnly periodStart, DateOnly periodEnd, DateOnly arrived, DateOnly? left)
    {
        var from = arrived > periodStart ? arrived : periodStart;
        var departure = left ?? periodEnd;
        var to = departure < periodEnd ? departure : periodEnd;

        if (to < from)
        {
            return 0m;
        }

        return to.DayNumber - from.DayNumber + 1;
    }
}
