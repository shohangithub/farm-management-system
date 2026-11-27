namespace Farm360.Application.Weather;

// ══════════════════════════════════════════════════════════════════════════════
// Provider-neutral forecast. Units: °C, % relative humidity, mm, km/h.
// Times are the farm's local wall-clock time (the provider is asked for Asia/Dhaka).
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>Conditions right now.</summary>
public sealed record WeatherNow(
    DateTime Time,
    double TemperatureC,
    double HumidityPercent,
    double ApparentTemperatureC,
    double PrecipitationMm,
    int WeatherCode,
    double WindSpeedKmh,
    double WindGustsKmh);

/// <summary>One forecast hour.</summary>
public sealed record WeatherHour(
    DateTime Time,
    double TemperatureC,
    double HumidityPercent,
    int? PrecipitationProbability);

/// <summary>One forecast day.</summary>
public sealed record WeatherDay(
    DateOnly Date,
    double MaxTemperatureC,
    double MinTemperatureC,
    double PrecipitationSumMm,
    int? PrecipitationProbabilityMax,
    int WeatherCode,
    double WindSpeedMaxKmh,
    double? WindGustsMaxKmh,
    double? HumidityMeanPercent,
    DateTime? Sunrise,
    DateTime? Sunset);

/// <summary>What a weather provider returns for one point.</summary>
public sealed record WeatherForecast(
    double Latitude,
    double Longitude,
    string TimeZone,
    int UtcOffsetSeconds,
    WeatherNow Current,
    IReadOnlyList<WeatherHour> Hourly,
    IReadOnlyList<WeatherDay> Daily);

/// <summary>
/// A weather forecast source (Open-Meteo by default; see <c>Weather:Provider</c>). Implemented in
/// Infrastructure. Only the coordinates are sent: never a farm name, id or user.
/// </summary>
public interface IWeatherProvider
{
    /// <summary>Short name shown as the data source ("Open-Meteo").</summary>
    string Name { get; }

    /// <summary>Current, hourly (about 7 days) and daily (7 days) forecast for the point.</summary>
    /// <exception cref="WeatherUnavailableException">The provider could not be reached or answered badly.</exception>
    Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default);
}

/// <summary>The provider failed (network, timeout, bad answer). Callers show "Weather unavailable".</summary>
public sealed class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException() { }

    public WeatherUnavailableException(string message) : base(message) { }

    public WeatherUnavailableException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>Configuration section <c>Weather</c>.</summary>
public sealed class WeatherOptions
{
    public const string SectionName = "Weather";

    /// <summary>Which provider: "OpenMeteo" (the only one today).</summary>
    public string Provider { get; set; } = "OpenMeteo";

    /// <summary>
    /// Forecast endpoint. The free endpoint (https://api.open-meteo.com/v1/forecast) is for
    /// non-commercial use only; commercial production use needs an Open-Meteo customer plan
    /// (https://customer-api.open-meteo.com/v1/forecast with <see cref="ApiKey"/>) or another provider.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.open-meteo.com/v1/forecast";

    /// <summary>Optional key (customer plan). Secret: set it through user secrets / environment, never in appsettings.</summary>
    public string? ApiKey { get; set; }

    /// <summary>How long one point's forecast is reused.</summary>
    public int CacheMinutes { get; set; } = 30;

    /// <summary>Off: the weather card says "Weather unavailable" and no alerts are made.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Per-request timeout of the provider call.</summary>
    public int TimeoutSeconds { get; set; } = 10;
}
