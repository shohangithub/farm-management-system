namespace Farm360.Application.Weather;

/// <summary>How urgent an advice item is. "Good" is good news (a good day for work).</summary>
public enum WeatherAdviceSeverity
{
    Good = 0,
    Info = 1,
    Warning = 2,
    Danger = 3,
}

/// <summary>When an advice item applies.</summary>
public static class WeatherWindows
{
    public const string Today = "today";
    public const string Tomorrow = "tomorrow";
    public const string Later = "later";

    public static string For(DateOnly date, DateOnly today) =>
        date <= today ? Today : date == today.AddDays(1) ? Tomorrow : Later;
}

public sealed record WeatherLocationDto(double Latitude, double Longitude);

public sealed record WeatherCurrentDto(
    DateTime Time,
    double TemperatureC,
    double FeelsLikeC,
    double HumidityPercent,
    double PrecipitationMm,
    double WindSpeedKmh,
    double WindGustsKmh,
    int WeatherCode,
    string Condition,
    string ConditionBn,
    string Icon,
    int Thi,
    HeatStressBand Band);

public sealed record WeatherHourDto(
    DateTime Time,
    double TemperatureC,
    double HumidityPercent,
    int? RainChance,
    int Thi,
    HeatStressBand Band);

/// <summary>One day. <c>HotFrom</c>/<c>HotTo</c>: the hours ("11:00"–"16:00") when the THI is moderate or worse (null when never).</summary>
public sealed record WeatherDayDto(
    DateOnly Date,
    double MaxTemperatureC,
    double MinTemperatureC,
    double RainMm,
    int? RainChance,
    int WeatherCode,
    string Condition,
    string ConditionBn,
    string Icon,
    double WindMaxKmh,
    double? GustMaxKmh,
    double? HumidityPercent,
    int PeakThi,
    HeatStressBand PeakBand,
    string? HotFrom,
    string? HotTo);

/// <summary>
/// One piece of advice. <see cref="Title"/> and <see cref="Action"/> are English texts that are also the
/// translation keys (the web shows them through its dictionary), with <c>{name}</c> placeholders filled
/// from <see cref="Params"/>.
/// </summary>
public sealed record WeatherAdviceDto(
    string Code,
    string Rule,
    WeatherAdviceSeverity Severity,
    string Icon,
    string Title,
    string Action,
    IReadOnlyDictionary<string, string> Params,
    string Window,
    DateOnly FromDate,
    DateOnly ToDate);

/// <summary>The suggested hours for outdoor work (lowest THI, daylight, low rain chance).</summary>
public sealed record WeatherBestHoursDto(DateOnly Date, bool IsToday, IReadOnlyList<WeatherHourDto> Hours);

public sealed record WeatherHeatPeakDto(DateTime Time, int Thi, HeatStressBand Band);

/// <summary>A bell-worthy event today or tomorrow: severe heat, heavy rain, a cold wave or a storm.</summary>
public sealed record WeatherAlertDto(string Kind, string Window, DateOnly Date, string Value);

/// <summary>Everything worked out from one forecast. <c>MiddayThi</c>: worst THI between 12:00 and 15:00 today (Today's-tasks hint).</summary>
public sealed record WeatherReportDto(
    DateTime LocalNow,
    DateOnly Today,
    WeatherCurrentDto Current,
    WeatherHeatPeakDto? Peak48Hours,
    int? MiddayThi,
    HeatStressBand? MiddayBand,
    IReadOnlyList<WeatherHourDto> Hourly,
    IReadOnlyList<WeatherDayDto> Daily,
    IReadOnlyList<WeatherAdviceDto> Advice,
    IReadOnlyList<WeatherAdviceDto> TopAdvice,
    WeatherBestHoursDto? BestHours,
    IReadOnlyList<WeatherAlertDto> Alerts);

/// <summary>
/// GET /api/v1/farms/{farmId}/weather. <see cref="NeedsLocation"/>: the farm has no coordinates yet.
/// <see cref="Available"/> false: weather is switched off or the provider failed ("Weather unavailable").
/// </summary>
public sealed record FarmWeatherDto(
    Guid FarmId,
    Guid BranchId,
    bool NeedsLocation,
    bool Available,
    string? Provider,
    WeatherLocationDto? Location,
    DateTime? FetchedAtUtc,
    WeatherReportDto? Report)
{
    public static FarmWeatherDto MissingLocation(Guid farmId, Guid branchId) =>
        new(farmId, branchId, true, false, null, null, null, null);

    public static FarmWeatherDto Unavailable(Guid farmId, Guid branchId, WeatherLocationDto? location) =>
        new(farmId, branchId, false, false, null, location, null, null);
}
