using System;
using Farm360.Contracts.Intelligence;

namespace Farm360.Application.Intelligence.Services;

/// <summary>
/// The investment a projection that starts <b>today</b> begins from.
/// </summary>
/// <remarks>
/// The projection's day 0 is the animal's latest weight, i.e. now, so its day-0 investment has
/// to be everything already spent on the animal — the cost to date — not the bare purchase
/// price. Using the purchase price leaves out all feed, vet and labour to date and overstates
/// every projected profit. Same convention as GetProjectionDefaultsQuery.
/// </remarks>
public static class ProjectionInputsFactory
{
    /// <summary>The money sunk into the animal at day 0: cost to date, else the purchase price.</summary>
    public static decimal InvestmentToDate(ProjectionDefaultsDto defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        return defaults.CostToDateBdt is { Value: > 0 } costToDate
            ? Math.Max(costToDate.Value, defaults.PurchasePriceBdt.Value)
            : defaults.PurchasePriceBdt.Value;
    }
}
