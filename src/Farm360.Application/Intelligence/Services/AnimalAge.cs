using System;

namespace Farm360.Application.Intelligence.Services;

/// <summary>
/// Animal age in calendar months, the one definition every report and screen uses.
/// </summary>
/// <remarks>
/// Counts whole calendar months from date of birth (an animal born on 31 Jan is one month old
/// on 28/29 Feb), rather than days ÷ 30, which drifts by about five days a year and printed
/// "12m" for an animal a few days short of its second birthday.
/// </remarks>
public static class AnimalAge
{
    /// <summary>Whole calendar months completed between <paramref name="dateOfBirth"/> and <paramref name="asOf"/>.</summary>
    public static int CompletedMonths(DateOnly dateOfBirth, DateOnly asOf)
    {
        if (asOf <= dateOfBirth)
        {
            return 0;
        }

        var months = ((asOf.Year - dateOfBirth.Year) * 12) + asOf.Month - dateOfBirth.Month;

        // The month is not complete until the birth day-of-month comes round, or until the end of
        // a shorter month that has no such day.
        var lastDayOfMonth = DateTime.DaysInMonth(asOf.Year, asOf.Month);
        if (asOf.Day < dateOfBirth.Day && asOf.Day < lastDayOfMonth)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    /// <summary>Days of life up to <paramref name="asOf"/> (never negative).</summary>
    public static int Days(DateOnly dateOfBirth, DateOnly asOf) =>
        Math.Max(0, asOf.DayNumber - dateOfBirth.DayNumber);

    /// <summary>
    /// Compact label: "12 d" under a month, "7 mo" under a year, otherwise "2y" or "2y 3m".
    /// The month remainder is always 0–11, so "12m" can never appear.
    /// </summary>
    public static string Format(DateOnly dateOfBirth, DateOnly asOf)
    {
        var months = CompletedMonths(dateOfBirth, asOf);
        if (months == 0)
        {
            return $"{Days(dateOfBirth, asOf)} d";
        }

        if (months < 12)
        {
            return $"{months} mo";
        }

        var years = months / 12;
        var remainder = months % 12;
        return remainder > 0 ? $"{years}y {remainder}m" : $"{years}y";
    }
}
