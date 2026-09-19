using System;

namespace Farm360.Domain.Common;

/// <summary>
/// Divides an amount between parties in proportion to weighting factors, without losing a unit.
/// </summary>
/// <remarks>
/// <para>
/// Shared by feed allocation and overhead allocation so there is exactly one implementation of
/// "split this money fairly and make the parts add back to the whole". Every per-animal cost in
/// Farm360 passes through here.
/// </para>
/// <para>
/// Uses the largest-remainder method. Naive proportional rounding strands a few paisa on nearly
/// every split, and those fractions accumulate into a permanent, unexplainable gap between the
/// sum of animal costs and the farm's actual spend — the kind of drift that quietly destroys
/// confidence in a report pack.
/// </para>
/// </remarks>
public static class ProportionalSplit
{
    /// <summary>
    /// Splits <paramref name="total"/> across <paramref name="factors"/>, rounded to
    /// <paramref name="decimals"/> places. The returned parts sum exactly to the total.
    /// </summary>
    /// <remarks>
    /// Callers must pass factors in a stable order (sort by id, not by whatever the database
    /// returned), because the tie-break for leftover units is positional — otherwise the same
    /// inputs could split differently between a job run and a later re-run.
    /// </remarks>
    public static decimal[] Distribute(decimal total, decimal[] factors, int decimals)
    {
        ArgumentNullException.ThrowIfNull(factors);

        var parts = new decimal[factors.Length];
        if (factors.Length == 0 || total == 0m)
        {
            return parts;
        }

        decimal factorTotal = 0m;
        for (var i = 0; i < factors.Length; i++)
        {
            factorTotal += factors[i];
        }

        // Degenerate input (every factor zero): fall back to an equal split rather than dividing
        // by zero or silently dropping the amount.
        if (factorTotal <= 0m)
        {
            for (var i = 0; i < factors.Length; i++)
            {
                factors[i] = 1m;
            }

            factorTotal = factors.Length;
        }

        var step = Step(decimals);
        var remainders = new (int Index, decimal Fraction)[factors.Length];
        decimal assigned = 0m;

        for (var i = 0; i < factors.Length; i++)
        {
            var exact = total * factors[i] / factorTotal;

            // Truncate toward zero so a negative total (a correction) behaves symmetrically.
            var floored = decimal.Truncate(exact / step) * step;

            parts[i] = floored;
            assigned += floored;
            remainders[i] = (i, Math.Abs(exact - floored));
        }

        var residual = total - assigned;
        if (residual == 0m)
        {
            return parts;
        }

        var direction = residual > 0m ? step : -step;
        var units = (int)Math.Round(Math.Abs(residual) / step, MidpointRounding.AwayFromZero);

        Array.Sort(remainders, static (a, b) =>
        {
            var byFraction = b.Fraction.CompareTo(a.Fraction);
            return byFraction != 0 ? byFraction : a.Index.CompareTo(b.Index);
        });

        for (var i = 0; i < units; i++)
        {
            parts[remainders[i % remainders.Length].Index] += direction;
        }

        return parts;
    }

    private static decimal Step(int decimals) => decimals switch
    {
        0 => 1m,
        1 => 0.1m,
        2 => 0.01m,
        3 => 0.001m,
        4 => 0.0001m,
        _ => 1m / (decimal)Math.Pow(10, decimals),
    };
}
