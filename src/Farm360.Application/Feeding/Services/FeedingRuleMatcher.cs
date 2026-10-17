using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;

namespace Farm360.Application.Feeding.Services;

/// <summary>Where the animal sat against the rule set's ranges.</summary>
public enum RuleMatchFit
{
    /// <summary>Inside a range, or the rule set has no ranges (fixed quantity).</summary>
    InRange,

    /// <summary>Below the lowest range: the lowest range's ration was used.</summary>
    BelowRange,

    /// <summary>Beyond the top of the highest range: the highest range's ration was used.</summary>
    AboveRange,

    /// <summary>Weight or age unknown: the lowest range's ration was used.</summary>
    Unknown,
}

public sealed record RuleMatch(IReadOnlyList<FeedingRuleLine> Lines, RuleMatchFit Fit, decimal? Measure)
{
    /// <summary>The first matched line (the one a plan records as its current rule).</summary>
    public FeedingRuleLine? Primary => Lines.Count > 0 ? Lines[0] : null;
}

/// <summary>
/// The one place that decides which rule lines of a rule set feed an animal today, so the
/// assignment, the daily job, the weight-recorded handler and the plan list always agree.
/// </summary>
/// <remarks>
/// <para>
/// Ranges are read as a ladder: an animal is on the highest range whose start ("from") it has
/// reached, and stays there until it reaches the next range's start. That makes the form's
/// inclusive ranges (81–150, 151–180) work for every weight, including 150 kg, 150.5 kg and
/// anything that falls in a gap between two ranges — none of which drop back to the lowest range.
/// </para>
/// <para>
/// Weight plans (fixed kg or % of body weight) use the latest weight; age plans use the age in
/// days on the feeding date; fixed-quantity plans feed every line. Lines that share a start
/// (e.g. concentrate and roughage for the same range) are all returned.
/// </para>
/// </remarks>
public static class FeedingRuleMatcher
{
    public static RuleMatch Match(FeedingRuleSet ruleSet, decimal? weightKg, int? ageDays)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        var lines = ruleSet.Lines.ToList();
        if (lines.Count == 0)
            return new RuleMatch([], RuleMatchFit.InRange, null);

        return ruleSet.PlanType switch
        {
            FeedingPlanType.FixedQuantity => new RuleMatch(lines, RuleMatchFit.InRange, null),
            FeedingPlanType.AgeBased => Ladder(lines, ageDays,
                l => l.MinAgeDays ?? 0m, l => l.MaxAgeDays is { } max ? max : null),
            _ => Ladder(lines, weightKg is > 0 ? weightKg : null,
                l => l.MinWeightKg ?? l.WeightFromKg, l => l.MaxWeightKg ?? (l.WeightToKg > 0 ? l.WeightToKg : null)),
        };
    }

    /// <summary>Daily kg for a line: % of body weight for percentage plans, otherwise the line's kg.</summary>
    public static decimal ExpectedKg(FeedingRuleSet ruleSet, FeedingRuleLine line, decimal weightKg)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(line);
        return ruleSet.PlanType == FeedingPlanType.WeightPercentage
            ? weightKg * line.ConcentrateKgPerDay / 100m
            : line.ConcentrateKgPerDay;
    }

    /// <summary>Age in whole days on <paramref name="day"/>, or null when the birth date is unusable.</summary>
    public static int? AgeInDays(DateOnly dateOfBirth, DateOnly day) =>
        dateOfBirth == default || dateOfBirth > day ? null : day.DayNumber - dateOfBirth.DayNumber;

    private static RuleMatch Ladder(
        List<FeedingRuleLine> lines,
        decimal? measure,
        Func<FeedingRuleLine, decimal> start,
        Func<FeedingRuleLine, decimal?> end)
    {
        var steps = lines
            .GroupBy(start)
            .OrderBy(g => g.Key)
            .Select(g => (Start: g.Key, Lines: (IReadOnlyList<FeedingRuleLine>)g.ToList()))
            .ToList();

        if (measure is not { } value)
            return new RuleMatch(steps[0].Lines, RuleMatchFit.Unknown, null);

        var reached = steps.LastOrDefault(s => s.Start <= value);
        if (reached.Lines is null)
            return new RuleMatch(steps[0].Lines, RuleMatchFit.BelowRange, value);

        // Past the end of the top range (when it has one): still the top ration, but say so.
        var isTop = reached.Start == steps[^1].Start;
        var topEnd = isTop && reached.Lines.All(l => end(l).HasValue) ? reached.Lines.Max(l => end(l)!.Value) : (decimal?)null;
        var fit = topEnd is { } limit && value >= limit + 1m ? RuleMatchFit.AboveRange : RuleMatchFit.InRange;
        return new RuleMatch(reached.Lines, fit, value);
    }
}
