using System;

namespace Farm360.Domain.Finance.Accounting;

public static class FiscalYear
{
    /// <summary>Bangladesh's fiscal year (July–June), used when a farm hasn't chosen one.</summary>
    public const int DefaultStartMonth = 7;

    /// <summary>First day of the fiscal year that contains <paramref name="day"/>.</summary>
    public static DateOnly StartOf(DateOnly day, int startMonth)
    {
        var month = startMonth is >= 1 and <= 12 ? startMonth : DefaultStartMonth;
        var start = new DateOnly(day.Year, month, 1);
        return start > day ? start.AddYears(-1) : start;
    }

    /// <summary>Last day of the fiscal year that contains <paramref name="day"/>.</summary>
    public static DateOnly EndOf(DateOnly day, int startMonth) => StartOf(day, startMonth).AddYears(1).AddDays(-1);
}
