using System;
using System.Collections.Generic;
using System.Globalization;
using Farm360.Application.Common.Time;

namespace Farm360.Application.Reporting.Services;

/// <summary>
/// Resolves the time zone a report is printed in and formats the generation stamp.
/// Zone resolution is shared with the rest of the platform via <see cref="FarmTimeZones"/> / <c>IFarmClock</c>.
/// </summary>
/// <remarks>
/// <para>
/// Farm360 runs on Linux containers in production and on Windows on developer machines, and the
/// two disagree about zone ids: Linux knows "Asia/Dhaka", Windows knows "Bangladesh Standard Time".
/// .NET translates between them when ICU is present, but a trimmed container may lack ICU or the
/// tz database entirely. A printed report must never fail because of that, so resolution walks
/// IANA → Windows → the Bangladesh default → a fixed UTC+6 zone, and always returns something.
/// </para>
/// <para>
/// Bangladesh has not observed daylight saving since 2009, so the fixed UTC+6 fallback is exact
/// for the market this ships to.
/// </para>
/// </remarks>
public static class ReportTimeZone
{
    public const string DefaultIanaId = FarmTimeZones.DefaultIanaId;
    public const string DefaultWindowsId = FarmTimeZones.DefaultWindowsId;

    /// <summary>
    /// Well-known abbreviations. .NET does not expose zone abbreviations, and "BST" for Bangladesh
    /// is what is printed on every receipt in the country.
    /// </summary>
    private static readonly Dictionary<string, string> Abbreviations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Dhaka"] = "BST",
        ["Bangladesh Standard Time"] = "BST",
        ["Asia/Kolkata"] = "IST",
        ["Asia/Calcutta"] = "IST",
        ["India Standard Time"] = "IST",
        ["UTC"] = "UTC",
        ["Etc/UTC"] = "UTC",
    };

    /// <summary>The Bangladesh zone, resolved once. Delegates to <see cref="FarmTimeZones"/>.</summary>
    public static TimeZoneInfo Default => FarmTimeZones.Default;

    /// <summary>
    /// The first of <paramref name="candidates"/> this machine can resolve, or <see cref="Default"/>.
    /// Blank and unknown ids are skipped rather than thrown on. Delegates to <see cref="FarmTimeZones"/>.
    /// </summary>
    public static TimeZoneInfo Resolve(params string?[] candidates) => FarmTimeZones.Resolve(candidates);

    /// <summary>"Today" as the farm sees it.</summary>
    public static DateOnly Today(DateTime utcNow, TimeZoneInfo zone) => FarmTimeZones.Today(utcNow, zone);

    public static DateTime ToLocal(DateTime utcNow, TimeZoneInfo zone) => FarmTimeZones.ToLocal(utcNow, zone);

    /// <summary>"BST (UTC+6)", "IST (UTC+5:30)", or "UTC+6" for a zone without a known abbreviation.</summary>
    public static string Label(TimeZoneInfo zone, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var offset = FormatOffset(zone.GetUtcOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)));

        var abbreviation = Abbreviations.TryGetValue(zone.Id, out var a) ? a : null;
        if (abbreviation is null && TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana))
        {
            Abbreviations.TryGetValue(iana, out abbreviation);
        }

        return abbreviation is null || abbreviation == "UTC" && offset == "UTC"
            ? offset
            : $"{abbreviation} ({offset})";
    }

    /// <summary>"16-Nov-2026 14:05 BST (UTC+6)".</summary>
    public static string FormatStamp(DateTime utcNow, TimeZoneInfo zone) =>
        $"{ToLocal(utcNow, zone).ToString("dd-MMM-yyyy HH:mm", CultureInfo.InvariantCulture)} {Label(zone, utcNow)}";

    private static string FormatOffset(TimeSpan offset)
    {
        if (offset == TimeSpan.Zero)
        {
            return "UTC";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return abs.Minutes == 0
            ? $"UTC{sign}{abs.Hours.ToString(CultureInfo.InvariantCulture)}"
            : $"UTC{sign}{abs.Hours.ToString(CultureInfo.InvariantCulture)}:{abs.Minutes.ToString("00", CultureInfo.InvariantCulture)}";
    }
}
