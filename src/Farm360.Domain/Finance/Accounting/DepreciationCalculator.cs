using System;

namespace Farm360.Domain.Finance.Accounting;

public enum DepreciationMethod
{
    /// <summary>Each fiscal year the asset loses a fixed % of its value at the start of that year (tax/auditor norm).</summary>
    ReducingBalance = 0,

    /// <summary>An equal charge each year over the remaining useful life, down to the residual value.</summary>
    StraightLine = 1,
}

/// <param name="FromDay">First day depreciation is charged for.</param>
/// <param name="Value">Value at the start of <paramref name="FromDay"/> (cost, or the opening written-down value).</param>
/// <param name="RatePercent">Reducing balance: % per fiscal year.</param>
/// <param name="UsefulLifeYears">Straight line: remaining useful life from <paramref name="FromDay"/>.</param>
public readonly record struct DepreciationBasis(
    DateOnly FromDay,
    decimal Value,
    DepreciationMethod Method,
    decimal RatePercent,
    decimal UsefulLifeYears,
    decimal ResidualValue);

/// <summary>
/// Written-down value of a fixed asset, matching an audited fixed-asset schedule: fiscal-year based,
/// with a part year charged by days. A zero rate (or zero life) never depreciates — land, for example.
/// </summary>
public static class DepreciationCalculator
{
    /// <summary>Value at the end of <paramref name="asOfDay"/>, rounded to the paisa.</summary>
    public static decimal ValueAtEndOf(DepreciationBasis basis, DateOnly asOfDay, int fiscalYearStartMonth)
    {
        var toDay = asOfDay.AddDays(1); // exclusive
        if (toDay <= basis.FromDay)
        {
            return Math.Round(basis.Value, 2);
        }

        return basis.Method == DepreciationMethod.StraightLine
            ? StraightLine(basis, toDay, fiscalYearStartMonth)
            : ReducingBalance(basis, toDay, fiscalYearStartMonth);
    }

    private static decimal ReducingBalance(DepreciationBasis basis, DateOnly toDay, int fyStartMonth)
    {
        if (basis.RatePercent <= 0)
        {
            return Math.Round(basis.Value, 2);
        }

        var rate = basis.RatePercent / 100m;
        var value = basis.Value;
        foreach (var (days, yearDays) in Segments(basis.FromDay, toDay, fyStartMonth))
        {
            // The rate applies to the value at the start of each fiscal year; within a year it is
            // charged evenly by days, so the year's charge equals value × rate.
            value -= value * rate * days / yearDays;
        }

        return Math.Round(value, 2);
    }

    private static decimal StraightLine(DepreciationBasis basis, DateOnly toDay, int fyStartMonth)
    {
        var depreciable = basis.Value - basis.ResidualValue;
        if (basis.UsefulLifeYears <= 0 || depreciable <= 0)
        {
            return Math.Round(basis.Value, 2);
        }

        var annual = depreciable / basis.UsefulLifeYears;
        var charged = 0m;
        foreach (var (days, yearDays) in Segments(basis.FromDay, toDay, fyStartMonth))
        {
            charged += annual * days / yearDays;
        }

        return Math.Round(basis.Value - Math.Min(charged, depreciable), 2);
    }

    /// <summary>The [from, to) range cut at fiscal-year boundaries: (days in segment, days in that fiscal year).</summary>
    private static System.Collections.Generic.IEnumerable<(decimal Days, decimal YearDays)> Segments(
        DateOnly from, DateOnly toExclusive, int fyStartMonth)
    {
        var current = from;
        while (current < toExclusive)
        {
            var yearStart = FiscalYear.StartOf(current, fyStartMonth);
            var yearEnd = yearStart.AddYears(1); // exclusive
            var segmentEnd = yearEnd < toExclusive ? yearEnd : toExclusive;
            yield return (segmentEnd.DayNumber - current.DayNumber, yearEnd.DayNumber - yearStart.DayNumber);
            current = segmentEnd;
        }
    }
}
