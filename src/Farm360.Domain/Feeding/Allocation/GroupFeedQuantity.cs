using System;
using Farm360.Domain.BusinessRules;

namespace Farm360.Domain.Feeding.Allocation;

/// <summary>
/// How much feed a feeding entry's quantity stands for. A ration on an animal's own plan feeds that
/// animal. A group (batch / shed / pen) plan's quantity is either one head's ration
/// (<see cref="GroupPlanQuantityBasis.PerHead"/>, the default) or the whole group's total
/// (<see cref="GroupPlanQuantityBasis.WholeGroup"/>). Stock deduction, cost and per-animal
/// allocation must all read it the same way, so they all go through here.
/// </summary>
public static class GroupFeedQuantity
{
    /// <summary>What an entry's quantity is multiplied by to get the feed actually given.</summary>
    public static decimal HeadMultiplier(bool isGroupPlan, int headCount, GroupPlanQuantityBasis basis) =>
        isGroupPlan && basis == GroupPlanQuantityBasis.PerHead ? Math.Max(1, headCount) : 1m;

    /// <summary>The feed actually given for an entry quantity.</summary>
    public static decimal TotalKg(decimal entryKg, bool isGroupPlan, int headCount, GroupPlanQuantityBasis basis) =>
        entryKg * HeadMultiplier(isGroupPlan, headCount, basis);
}
