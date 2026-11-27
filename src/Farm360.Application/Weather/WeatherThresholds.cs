namespace Farm360.Application.Weather;

/// <summary>
/// Every number the cattle weather advice uses, in one place.
/// <para>
/// Sources (general veterinary / extension guidance, not farm-specific prescriptions):
/// <list type="bullet">
///   <item>THI formula and bands: NRC (1971) "A guide to environmental research on animals" THI;
///   beef-cattle stress bands as used by the Livestock Weather Safety Index (LCI 1970) and later
///   extension material (e.g. Mader et al. 2006, J. Anim. Sci. 84:712; university extension heat-stress
///   guides): below 72 comfortable, 72–78 mild (alert), 79–88 moderate (danger to weak animals),
///   89–98 severe, above 98 emergency.</item>
///   <item>Heavy rain: Bangladesh Meteorological Department classes 23–43 mm/day as "moderately heavy";
///   20 mm or a 70% chance is used as the practical "prepare the shed" trigger.</item>
///   <item>Cold: BMD calls 8–10 °C a moderate and 10 °C+ a mild cold wave; calves and weak animals
///   need cover below about 12 °C at night (DLS winter-care advice).</item>
///   <item>Storm: gusts of 50 km/h and more (Beaufort 7, "near gale") lift tin sheets; WMO weather codes
///   95/96/99 are thunderstorms (Nor'westers / kalbaishakhi in spring).</item>
///   <item>Humid warmth: several days above about 80% humidity and 28 °C favour fungal skin and hoof
///   problems and mould in stored feed.</item>
/// </list>
/// </para>
/// </summary>
public static class WeatherThresholds
{
    // ── Temperature-Humidity Index bands (beef cattle), on the THI rounded to a whole number ──
    public const int ThiMildFrom = 72;
    public const int ThiModerateFrom = 79;
    public const int ThiSevereFrom = 89;
    public const int ThiDangerFrom = 99; // "above 98"

    // ── Rain ─────────────────────────────────────────────────────────────────────────────────
    public const int HeavyRainProbabilityPercent = 70;
    public const double HeavyRainMm = 20;

    // ── Cold ─────────────────────────────────────────────────────────────────────────────────
    public const double ColdMinTemperatureC = 12;

    // ── Wind / storm ───────────────────────────────────────────────────────────────────────────
    public const double StormGustKmh = 50;

    // ── Humid warmth for several days ─────────────────────────────────────────────────────────
    public const double HumidPercent = 80;
    public const double HumidWarmMaxTemperatureC = 28;
    public const int HumidWarmDays = 3;

    // ── Work planning ──────────────────────────────────────────────────────────────────────────
    /// <summary>Hours with a higher chance of rain are not "best hours" for outdoor work.</summary>
    public const int BestHoursMaxRainProbabilityPercent = 40;

    /// <summary>How many best hours to suggest.</summary>
    public const int BestHoursCount = 3;

    /// <summary>Daylight when the provider gives no sunrise/sunset.</summary>
    public const int DaylightFromHour = 6;
    public const int DaylightToHour = 18;

    /// <summary>The "midday" window checked for the Today's-tasks hint (12:00–14:59).</summary>
    public const int MiddayFromHour = 12;
    public const int MiddayToHour = 14;

    /// <summary>Hot-day hint: do the work before this hour.</summary>
    public const int MorningWorkBeforeHour = 10;

    /// <summary>Hours of hourly forecast returned to the web.</summary>
    public const int HourlyHoursShown = 48;
}
