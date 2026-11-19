using System;

namespace Farm360.Application.Common.Time;

/// <summary>
/// Resolves farm time zones from the ids stored on organizations and tenants.
/// </summary>
/// <remarks>
/// <para>
/// Farm360 runs on Linux containers in production and on Windows on developer machines, and the
/// two disagree about zone ids: Linux knows "Asia/Dhaka", Windows knows "Bangladesh Standard Time".
/// .NET translates between them when ICU is present, but a trimmed container may lack ICU or the
/// tz database entirely. Resolution must never fail because of that, so it walks
/// IANA → Windows → the Bangladesh default → a fixed UTC+6 zone, and always returns something.
/// </para>
/// <para>
/// Bangladesh has not observed daylight saving since 2009, so the fixed UTC+6 fallback is exact
/// for the market this ships to.
/// </para>
/// </remarks>
public static class FarmTimeZones
{
    public const string DefaultIanaId = "Asia/Dhaka";
    public const string DefaultWindowsId = "Bangladesh Standard Time";

    private static readonly TimeZoneInfo FixedBangladesh = TimeZoneInfo.CreateCustomTimeZone(
        DefaultIanaId, TimeSpan.FromHours(6), "Bangladesh Standard Time", "Bangladesh Standard Time");

    /// <summary>The Bangladesh zone, resolved once.</summary>
    public static TimeZoneInfo Default { get; } = ResolveCore(DefaultIanaId) ?? ResolveCore(DefaultWindowsId) ?? FixedBangladesh;

    /// <summary>
    /// The first of <paramref name="candidates"/> this machine can resolve, or <see cref="Default"/>.
    /// Blank and unknown ids are skipped rather than thrown on.
    /// </summary>
    public static TimeZoneInfo Resolve(params string?[] candidates)
    {
        if (candidates is not null)
        {
            foreach (var id in candidates)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var zone = ResolveCore(id.Trim());
                if (zone is not null)
                {
                    return zone;
                }
            }
        }

        return Default;
    }

    /// <summary>The wall-clock time in <paramref name="zone"/> at the UTC instant <paramref name="utcNow"/>.</summary>
    public static DateTime ToLocal(DateTime utcNow, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var utc = utcNow.Kind == DateTimeKind.Utc ? utcNow : DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
    }

    /// <summary>"Today" as a farm in <paramref name="zone"/> sees it at the UTC instant <paramref name="utcNow"/>.</summary>
    public static DateOnly Today(DateTime utcNow, TimeZoneInfo zone) => DateOnly.FromDateTime(ToLocal(utcNow, zone));

    private static TimeZoneInfo? ResolveCore(string id)
    {
        if (TryFind(id, out var zone))
        {
            return zone;
        }

        // The id is in the other OS's vocabulary — translate and try again.
        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id, out var windowsId) && TryFind(windowsId, out zone))
        {
            return zone;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id, out var ianaId) && TryFind(ianaId, out zone))
        {
            return zone;
        }

        if (string.Equals(id, DefaultIanaId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, DefaultWindowsId, StringComparison.OrdinalIgnoreCase))
        {
            return FixedBangladesh;
        }

        return null;
    }

    private static bool TryFind(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            zone = null!;
            return false;
        }
    }
}
