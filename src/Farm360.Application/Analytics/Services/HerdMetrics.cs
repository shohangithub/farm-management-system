using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Analytics.Services;

/// <summary>
/// Pure, unit-testable herd calculations shared by the dashboard queries. Nothing here invents a
/// value: when the data does not support a figure the result is <c>null</c> ("no data").
/// </summary>
public static class HerdMetrics
{
    /// <summary>A single weigh-in.</summary>
    public readonly record struct WeighIn(Guid AnimalId, DateOnly Date, decimal WeightKg);

    /// <summary>
    /// The span an animal was on the farm: from <paramref name="Entry"/> (inclusive) until
    /// <paramref name="Exit"/> (exclusive — it is not counted on the day it left). A null exit
    /// means it is still on the farm.
    /// </summary>
    public readonly record struct Residency(DateOnly Entry, DateOnly? Exit);

    /// <summary>
    /// The day an animal left the farm, or null while it is still present. Sale/death dates are
    /// authoritative; for an exit status without a recorded date (slaughtered/transferred) the
    /// last-modified date is the best available evidence.
    /// </summary>
    public static DateOnly? ExitDate(AnimalStatus status, DateOnly? saleDate, DateOnly? deathDate, DateTime? modifiedAtUtc)
    {
        if (status is AnimalStatus.Active or AnimalStatus.Quarantined)
        {
            return null;
        }

        return status switch
        {
            AnimalStatus.Sold => saleDate ?? deathDate ?? ToDate(modifiedAtUtc),
            AnimalStatus.Dead => deathDate ?? saleDate ?? ToDate(modifiedAtUtc),
            _ => deathDate ?? saleDate ?? ToDate(modifiedAtUtc),
        };
    }

    private static DateOnly? ToDate(DateTime? value) => value.HasValue ? DateOnly.FromDateTime(value.Value) : null;

    /// <summary>
    /// Average number of animals on the farm per day over [<paramref name="from"/>, <paramref name="to"/>]
    /// (both inclusive) = head-days ÷ days. Returns 0 for an empty or inverted window.
    /// </summary>
    public static decimal AverageHeadCount(IEnumerable<Residency> animals, DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        if (days <= 0)
        {
            return 0m;
        }

        long headDays = 0;
        foreach (var a in animals)
        {
            var start = Math.Max(a.Entry.DayNumber, from.DayNumber);
            var endExclusive = Math.Min(a.Exit?.DayNumber ?? int.MaxValue, to.DayNumber + 1);
            if (endExclusive > start)
            {
                headDays += endExclusive - start;
            }
        }

        return (decimal)headDays / days;
    }

    /// <summary>
    /// Herd average daily gain over [<paramref name="from"/>, <paramref name="to"/>]: for every animal with
    /// at least two weigh-ins in the window spanning at least one day, its own gain ÷ days between its first
    /// and last weigh-in; then the mean across those animals. Null when no animal qualifies.
    /// Weigh-ins of different animals are never combined.
    /// </summary>
    public static double? AverageDailyGain(IEnumerable<WeighIn> weighIns, DateOnly from, DateOnly to)
    {
        var perAnimal = new List<double>();
        foreach (var group in weighIns.Where(w => w.Date >= from && w.Date <= to).GroupBy(w => w.AnimalId))
        {
            var ordered = group.OrderBy(w => w.Date).ToList();
            var first = ordered[0];
            var last = ordered[^1];
            var days = last.Date.DayNumber - first.Date.DayNumber;
            if (ordered.Count < 2 || days < 1)
            {
                continue;
            }

            perAnimal.Add((double)(last.WeightKg - first.WeightKg) / days);
        }

        return perAnimal.Count == 0 ? null : Math.Round(perAnimal.Average(), 3);
    }
}
