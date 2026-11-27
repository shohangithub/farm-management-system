using System.Globalization;

namespace Farm360.Application.Weather;

/// <summary>
/// Turns a raw forecast into what a cattle farmer needs: THI and heat-stress band now, per hour and per
/// day, rule-based advice, the best work hours and the bell alerts. Pure: no I/O, no clock (the farm's
/// local time is passed in), so every rule is unit-tested.
/// </summary>
public static class WeatherAnalyzer
{
    /// <summary>Below this many hours on a day, its peak THI comes from the daily max temperature instead.</summary>
    private const int MinHoursForHourlyPeak = 12;

    /// <summary>Humidity assumed for a day's peak when the provider gives no daily humidity.</summary>
    private const double FallbackHumidityPercent = 60;

    public static WeatherReportDto Analyze(WeatherForecast forecast, DateTime localNow)
    {
        ArgumentNullException.ThrowIfNull(forecast);
        var today = DateOnly.FromDateTime(localNow);
        var thisHour = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, localNow.Kind);

        var allHours = forecast.Hourly.OrderBy(h => h.Time).Select(ToHour).ToList();
        var shown = allHours.Where(h => h.Time >= thisHour).Take(WeatherThresholds.HourlyHoursShown).ToList();

        var current = Current(forecast.Current);
        var peak = shown.Count == 0 ? null : shown.OrderByDescending(h => h.Thi).ThenBy(h => h.Time).Select(h => new WeatherHeatPeakDto(h.Time, h.Thi, h.Band)).First();

        var midday = allHours
            .Where(h => DateOnly.FromDateTime(h.Time) == today && h.Time.Hour >= WeatherThresholds.MiddayFromHour && h.Time.Hour <= WeatherThresholds.MiddayToHour)
            .ToList();
        int? middayThi = midday.Count == 0 ? null : midday.Max(h => h.Thi);
        HeatStressBand? middayBand = middayThi is { } m ? CattleHeatStress.Band(m) : null;

        var days = forecast.Daily.OrderBy(d => d.Date).Where(d => d.Date >= today).Select(d => Day(d, allHours)).ToList();
        var rawDays = forecast.Daily.Where(d => d.Date >= today).ToDictionary(d => d.Date);

        var advice = Advise(days, today);
        var top = TopAdvice(advice);
        var best = BestHours(allHours, rawDays, localNow);
        var alerts = Alerts(days, today);

        return new WeatherReportDto(localNow, today, current, peak, middayThi, middayBand, shown, days, advice, top, best, alerts);
    }

    // ── Building blocks ─────────────────────────────────────────────────────────────────────

    private static WeatherHourDto ToHour(WeatherHour h)
    {
        var thi = CattleHeatStress.Thi(h.TemperatureC, h.HumidityPercent);
        return new WeatherHourDto(h.Time, Round1(h.TemperatureC), Math.Round(h.HumidityPercent), h.PrecipitationProbability,
            CattleHeatStress.Rounded(thi), CattleHeatStress.Band(thi));
    }

    private static WeatherCurrentDto Current(WeatherNow now)
    {
        var condition = WeatherCodes.Describe(now.WeatherCode);
        var thi = CattleHeatStress.Thi(now.TemperatureC, now.HumidityPercent);
        return new WeatherCurrentDto(now.Time, Round1(now.TemperatureC), Round1(now.ApparentTemperatureC), Math.Round(now.HumidityPercent),
            Round1(now.PrecipitationMm), Round1(now.WindSpeedKmh), Round1(now.WindGustsKmh), now.WeatherCode,
            condition.English, condition.Bangla, condition.Icon, CattleHeatStress.Rounded(thi), CattleHeatStress.Band(thi));
    }

    /// <summary>
    /// A day's peak THI: the hottest forecast hour of that day (temperature and humidity of the same hour);
    /// when the day has too few hours, the daily max temperature with the day's mean humidity.
    /// </summary>
    internal static WeatherDayDto Day(WeatherDay d, IReadOnlyList<WeatherHourDto> allHours)
    {
        var hours = allHours.Where(h => DateOnly.FromDateTime(h.Time) == d.Date).ToList();
        int peakThi = hours.Count >= MinHoursForHourlyPeak
            ? hours.Max(h => h.Thi)
            : CattleHeatStress.Rounded(CattleHeatStress.Thi(d.MaxTemperatureC, d.HumidityMeanPercent ?? FallbackHumidityPercent));

        var hot = hours.Where(h => h.Band >= HeatStressBand.Moderate).OrderBy(h => h.Time).ToList();
        string? hotFrom = hot.Count == 0 ? null : Clock(hot[0].Time);
        string? hotTo = hot.Count == 0 ? null : Clock(hot[^1].Time.AddHours(1));

        var condition = WeatherCodes.Describe(d.WeatherCode);
        return new WeatherDayDto(d.Date, Round1(d.MaxTemperatureC), Round1(d.MinTemperatureC), Round1(d.PrecipitationSumMm),
            d.PrecipitationProbabilityMax, d.WeatherCode, condition.English, condition.Bangla, condition.Icon,
            Round1(d.WindSpeedMaxKmh), d.WindGustsMaxKmh is { } g ? Round1(g) : null,
            d.HumidityMeanPercent is { } rh ? Math.Round(rh) : null,
            peakThi, CattleHeatStress.Band(peakThi), hotFrom, hotTo);
    }

    // ── Conditions (each rule's trigger, shared by advice and alerts) ─────────────────────────

    public static bool IsHot(WeatherDayDto d) => d.PeakBand >= HeatStressBand.Moderate;

    public static bool IsSevereHeat(WeatherDayDto d) => d.PeakBand >= HeatStressBand.Severe;

    public static bool IsHeavyRain(WeatherDayDto d) =>
        (d.RainChance ?? 0) >= WeatherThresholds.HeavyRainProbabilityPercent || d.RainMm >= WeatherThresholds.HeavyRainMm;

    public static bool IsCold(WeatherDayDto d) => d.MinTemperatureC < WeatherThresholds.ColdMinTemperatureC;

    public static bool IsStorm(WeatherDayDto d) =>
        (d.GustMaxKmh ?? 0) >= WeatherThresholds.StormGustKmh || WeatherCodes.IsThunderstorm(d.WeatherCode);

    public static bool IsHumidWarm(WeatherDayDto d) =>
        (d.HumidityPercent ?? 0) >= WeatherThresholds.HumidPercent && d.MaxTemperatureC >= WeatherThresholds.HumidWarmMaxTemperatureC;

    public static bool IsGoodDay(WeatherDayDto d) =>
        d.PeakBand <= HeatStressBand.Mild && !IsHeavyRain(d) && !IsCold(d) && !IsStorm(d)
        && (d.RainChance ?? 0) < WeatherThresholds.BestHoursMaxRainProbabilityPercent;

    // ── Advice ──────────────────────────────────────────────────────────────────────────────

    private sealed record Rule(string Code, string RuleName, WeatherAdviceSeverity Severity, string Icon, string Title, string Action);

    // Heat (THI moderate or worse)
    private static readonly Rule HeatWater = new("heat.water", "heat", WeatherAdviceSeverity.Warning, "water_drop",
        "Give plenty of cool water", "Keep clean, cool water in front of every animal all day and refill the troughs often.");
    private static readonly Rule HeatFeed = new("heat.feed", "heat", WeatherAdviceSeverity.Warning, "schedule",
        "Feed in the cool hours", "Give concentrate early in the morning and in the evening, not at midday.");
    private static readonly Rule HeatShade = new("heat.shade", "heat", WeatherAdviceSeverity.Warning, "air",
        "Shade and fresh air", "Keep the animals in the shade; run fans or open the shed sides for air flow.");
    private static readonly Rule HeatBathe = new("heat.bathe", "heat", WeatherAdviceSeverity.Warning, "shower",
        "Cool the animals down", "Bathe the animals or sprinkle water on them in the afternoon.");
    private static readonly Rule HeatWorkWindow = new("heat.work", "heat", WeatherAdviceSeverity.Warning, "local_shipping",
        "No hard work in the heat", "Avoid transport, weighing and vaccination from {from} to {to}; do them in the morning.");
    private static readonly Rule HeatWork = new("heat.work", "heat", WeatherAdviceSeverity.Warning, "local_shipping",
        "No hard work in the heat", "Avoid transport, weighing and vaccination in the hot hours; do them in the morning.");

    // Severe heat or danger
    private static readonly Rule HeatSigns = new("heat.signs", "heat", WeatherAdviceSeverity.Danger, "emergency",
        "Watch for heat stress", "Check for fast panting, open-mouth breathing or drooling; call a vet if you see them.");
    private static readonly Rule HeatElectrolytes = new("heat.electrolytes", "heat", WeatherAdviceSeverity.Danger, "medication_liquid",
        "Add electrolytes to the water", "Add electrolyte powder or ORS to the drinking water.");

    // Heavy rain
    private static readonly Rule RainFloor = new("rain.floor", "rain", WeatherAdviceSeverity.Warning, "water_damage",
        "Keep the shed floor dry", "Keep the shed floor dry and change wet bedding.");
    private static readonly Rule RainDrains = new("rain.drains", "rain", WeatherAdviceSeverity.Warning, "plumbing",
        "Clear the drains", "Clear the drains around the shed before the rain.");
    private static readonly Rule RainFeed = new("rain.feed", "rain", WeatherAdviceSeverity.Warning, "inventory_2",
        "Keep feed and straw dry", "Store feed and straw covered and off the floor; damp feed grows mould.");
    private static readonly Rule RainPests = new("rain.pests", "rain", WeatherAdviceSeverity.Warning, "pest_control",
        "Ticks, flies and hooves", "Expect more ticks, flies and hoof problems; check the feet and the skin.");

    // Humid warmth for several days
    private static readonly Rule HumidSkin = new("humid.skin", "humid", WeatherAdviceSeverity.Info, "healing",
        "Skin and foot problems", "Warm, damp days: watch for fungal skin and foot problems and keep the floor dry.");
    private static readonly Rule HumidMould = new("humid.mould", "humid", WeatherAdviceSeverity.Info, "search",
        "Check feed for mould", "Check stored feed for mould; do not feed anything that smells or looks mouldy.");

    // Cold
    private static readonly Rule ColdCover = new("cold.cover", "cold", WeatherAdviceSeverity.Warning, "bed",
        "Keep calves warm", "Cover calves and weak animals with a jute sack or blanket, especially at night.");
    private static readonly Rule ColdDraught = new("cold.draught", "cold", WeatherAdviceSeverity.Warning, "window",
        "Block cold wind", "Block draughts: hang sacks or curtains on the open sides of the shed.");
    private static readonly Rule ColdFeed = new("cold.feed", "cold", WeatherAdviceSeverity.Warning, "grass",
        "A little more energy feed", "Give a little more energy feed, such as extra concentrate or molasses.");
    private static readonly Rule ColdNoBath = new("cold.nobath", "cold", WeatherAdviceSeverity.Warning, "do_not_disturb_on",
        "Do not bathe", "Do not bathe the animals on cold days; brush them instead.");

    // Strong wind / thunderstorm
    private static readonly Rule StormRoof = new("storm.roof", "storm", WeatherAdviceSeverity.Danger, "roofing",
        "Secure the roof", "Tie down the roof and tin sheets and move loose things away.");
    private static readonly Rule StormInside = new("storm.inside", "storm", WeatherAdviceSeverity.Danger, "home",
        "Keep animals inside", "Keep the animals inside the shed while the storm passes.");

    // Good day (links to the vet programme: bathe only if the weather allows; FMD morning/afternoon, healthy animals)
    private static readonly Rule GoodDay = new("good.day", "good", WeatherAdviceSeverity.Good, "thumb_up",
        "Good day for bathing, vaccination or transport",
        "Calm, mild weather: a good day to bathe, vaccinate or move animals. Give FMD in the morning or afternoon, to healthy animals only.");

    /// <summary>All advice for the forecast days, grouped today → tomorrow → later (later days merged per item).</summary>
    public static IReadOnlyList<WeatherAdviceDto> Advise(IReadOnlyList<WeatherDayDto> days, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(days);
        var items = new List<(int Order, WeatherAdviceDto Item, double Weight)>();

        void Add(Rule rule, WeatherDayDto day, Dictionary<string, string> parameters, double weight, DateOnly? to = null)
        {
            var window = WeatherWindows.For(day.Date, today);
            items.Add((items.Count, new WeatherAdviceDto(rule.Code, rule.RuleName, rule.Severity, rule.Icon, rule.Title, rule.Action,
                parameters, window, day.Date, to ?? day.Date), weight));
        }

        foreach (var day in days)
        {
            if (IsStorm(day))
            {
                var p = Params(("gust", Whole(day.GustMaxKmh ?? 0)));
                Add(StormRoof, day, p, day.GustMaxKmh ?? 0);
                Add(StormInside, day, p, day.GustMaxKmh ?? 0);
            }

            if (IsSevereHeat(day))
            {
                var p = Params(("thi", Whole(day.PeakThi)));
                Add(HeatSigns, day, p, day.PeakThi);
                Add(HeatElectrolytes, day, p, day.PeakThi);
            }

            if (IsHot(day))
            {
                var p = Params(("thi", Whole(day.PeakThi)));
                Add(HeatWater, day, p, day.PeakThi);
                Add(HeatFeed, day, p, day.PeakThi);
                Add(HeatShade, day, p, day.PeakThi);
                Add(HeatBathe, day, p, day.PeakThi);
                if (day.HotFrom is not null && day.HotTo is not null)
                    Add(HeatWorkWindow, day, Params(("thi", Whole(day.PeakThi)), ("from", day.HotFrom), ("to", day.HotTo)), day.PeakThi);
                else
                    Add(HeatWork, day, p, day.PeakThi);
            }

            if (IsHeavyRain(day))
            {
                var p = Params(("mm", Whole(day.RainMm)), ("chance", Whole(day.RainChance ?? 0)));
                var weight = Math.Max(day.RainMm, day.RainChance ?? 0);
                Add(RainFloor, day, p, weight);
                Add(RainDrains, day, p, weight);
                Add(RainFeed, day, p, weight);
                Add(RainPests, day, p, weight);
            }

            if (IsCold(day))
            {
                var p = Params(("min", Whole(day.MinTemperatureC)));
                Add(ColdCover, day, p, -day.MinTemperatureC);
                Add(ColdDraught, day, p, -day.MinTemperatureC);
                Add(ColdFeed, day, p, -day.MinTemperatureC);
                Add(ColdNoBath, day, p, -day.MinTemperatureC);
            }

            if (day.Date <= today.AddDays(1) && IsGoodDay(day))
                Add(GoodDay, day, Params(("thi", Whole(day.PeakThi))), 0);
        }

        // Humid warmth: a run of several such days (from today on) gets one pair of items for the run.
        foreach (var (first, last) in HumidRuns(days))
        {
            var start = days.First(d => d.Date == first);
            var p = Params(("days", Whole(last.DayNumber - first.DayNumber + 1)));
            Add(HumidSkin, start, p, 0, last);
            Add(HumidMould, start, p, 0, last);
        }

        // Later days: one item per advice code, covering the first to the last day it applies, with the
        // parameters of the worst of those days.
        var merged = items
            .GroupBy(i => i.Item.Window == WeatherWindows.Later ? $"later|{i.Item.Code}" : $"{i.Item.FromDate:yyyyMMdd}|{i.Item.Code}|{i.Order}")
            .Select(g =>
            {
                var worst = g.OrderByDescending(i => i.Weight).ThenBy(i => i.Order).First();
                var from = g.Min(i => i.Item.FromDate);
                var to = g.Max(i => i.Item.ToDate);
                return (Order: g.Min(i => i.Order), Item: worst.Item with { FromDate = from, ToDate = to });
            })
            .ToList();

        return merged
            .OrderBy(i => WindowRank(i.Item.Window))
            .ThenBy(i => i.Item.FromDate)
            .ThenByDescending(i => i.Item.Severity)
            .ThenBy(i => i.Order)
            .Select(i => i.Item)
            .ToList();
    }

    /// <summary>The 1–2 items for the dashboard: most urgent for today or tomorrow, today first on a tie.</summary>
    public static IReadOnlyList<WeatherAdviceDto> TopAdvice(IReadOnlyList<WeatherAdviceDto> advice, int take = 2)
    {
        ArgumentNullException.ThrowIfNull(advice);
        var near = advice.Where(a => a.Window != WeatherWindows.Later).ToList();
        var maxSeverity = near.Count == 0 ? WeatherAdviceSeverity.Good : near.Max(a => a.Severity);
        return near
            .Select((a, i) => (a, i))
            .OrderByDescending(x => x.a.Severity)
            .ThenBy(x => WindowRank(x.a.Window))
            .ThenBy(x => x.i)
            .Select(x => x.a)
            // A good-day note only shows when nothing else is going on.
            .Where(a => a.Severity != WeatherAdviceSeverity.Good || maxSeverity == WeatherAdviceSeverity.Good)
            .Take(take)
            .ToList();
    }

    /// <summary>Runs of at least <see cref="WeatherThresholds.HumidWarmDays"/> consecutive humid, warm days.</summary>
    internal static IReadOnlyList<(DateOnly First, DateOnly Last)> HumidRuns(IReadOnlyList<WeatherDayDto> days)
    {
        var runs = new List<(DateOnly First, DateOnly Last)>();
        DateOnly? first = null;
        DateOnly last = default;

        void Flush()
        {
            if (first is { } f && last.DayNumber - f.DayNumber + 1 >= WeatherThresholds.HumidWarmDays)
                runs.Add((f, last));
            first = null;
        }

        foreach (var day in days.OrderBy(d => d.Date))
        {
            if (!IsHumidWarm(day))
            {
                Flush();
                continue;
            }

            if (first is null || day.Date != last.AddDays(1))
            {
                Flush();
                first = day.Date;
            }

            last = day.Date;
        }

        Flush();
        return runs;
    }

    // ── Best hours ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The best hours left today for outdoor work (transport, weighing, vaccination, bathing): daylight
    /// hours from now on with a low rain chance, the lowest THI first; up to
    /// <see cref="WeatherThresholds.BestHoursCount"/>, returned in time order. When no hour is left today
    /// (evening), tomorrow's best hours instead.
    /// </summary>
    public static WeatherBestHoursDto? BestHours(IReadOnlyList<WeatherHourDto> hours, IReadOnlyDictionary<DateOnly, WeatherDay> days, DateTime localNow)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentNullException.ThrowIfNull(days);
        var today = DateOnly.FromDateTime(localNow);
        var thisHour = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, localNow.Kind);

        foreach (var date in new[] { today, today.AddDays(1) })
        {
            days.TryGetValue(date, out var day);
            var picked = hours
                .Where(h => DateOnly.FromDateTime(h.Time) == date && h.Time >= thisHour)
                .Where(h => IsDaylight(h.Time, day))
                .Where(h => (h.RainChance ?? 0) <= WeatherThresholds.BestHoursMaxRainProbabilityPercent)
                .OrderBy(h => h.Thi)
                .ThenBy(h => h.RainChance ?? 0)
                .ThenBy(h => h.Time)
                .Take(WeatherThresholds.BestHoursCount)
                .OrderBy(h => h.Time)
                .ToList();
            if (picked.Count > 0)
                return new WeatherBestHoursDto(date, date == today, picked);
        }

        return null;
    }

    private static bool IsDaylight(DateTime hourStart, WeatherDay? day)
    {
        if (day?.Sunrise is { } sunrise && day.Sunset is { } sunset)
            return hourStart >= sunrise.AddMinutes(-30) && hourStart.AddHours(1) <= sunset.AddMinutes(30);
        return hourStart.Hour >= WeatherThresholds.DaylightFromHour && hourStart.Hour < WeatherThresholds.DaylightToHour;
    }

    // ── Bell alerts ─────────────────────────────────────────────────────────────────────────

    public const string AlertStorm = "storm";
    public const string AlertHeat = "heat";
    public const string AlertRain = "rain";
    public const string AlertCold = "cold";

    /// <summary>
    /// Today's and tomorrow's events worth a bell notification, most dangerous first: storm, severe heat,
    /// heavy rain, cold. Mild/moderate heat, light rain and the like stay on the weather page only.
    /// </summary>
    public static IReadOnlyList<WeatherAlertDto> Alerts(IReadOnlyList<WeatherDayDto> days, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(days);
        var alerts = new List<WeatherAlertDto>();
        foreach (var day in days.Where(d => d.Date == today || d.Date == today.AddDays(1)).OrderBy(d => d.Date))
        {
            var window = WeatherWindows.For(day.Date, today);
            if (IsStorm(day)) alerts.Add(new(AlertStorm, window, day.Date, Whole(day.GustMaxKmh ?? day.WindMaxKmh)));
            if (IsSevereHeat(day)) alerts.Add(new(AlertHeat, window, day.Date, Whole(day.PeakThi)));
            if (IsHeavyRain(day)) alerts.Add(new(AlertRain, window, day.Date, Whole(day.RainMm)));
            if (IsCold(day)) alerts.Add(new(AlertCold, window, day.Date, Whole(day.MinTemperatureC)));
        }

        return alerts
            .OrderBy(a => Array.IndexOf(AlertOrder, a.Kind))
            .ThenBy(a => a.Date)
            .ToList();
    }

    private static readonly string[] AlertOrder = [AlertStorm, AlertHeat, AlertRain, AlertCold];

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static int WindowRank(string window) => window switch
    {
        WeatherWindows.Today => 0,
        WeatherWindows.Tomorrow => 1,
        _ => 2,
    };

    private static Dictionary<string, string> Params(params (string Key, string Value)[] values) =>
        values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

    private static string Whole(double value) =>
        Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private static string Clock(DateTime time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
