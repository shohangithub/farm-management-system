using System.Globalization;
using System.Text.Json;
using Farm360.Application.Weather;
using Microsoft.Extensions.Options;

namespace Farm360.Infrastructure.Weather;

/// <summary>
/// Open-Meteo forecast API (https://open-meteo.com). No key for the free endpoint, which is for
/// NON-COMMERCIAL use only: a commercial production deployment needs an Open-Meteo customer plan
/// (Weather:BaseUrl = https://customer-api.open-meteo.com/v1/forecast plus Weather:ApiKey from a secret
/// store) or another <see cref="IWeatherProvider"/>. Only the (rounded) coordinates are sent.
/// </summary>
public sealed class OpenMeteoWeatherProvider(HttpClient httpClient, IOptions<WeatherOptions> options) : IWeatherProvider
{
    public const string HttpClientName = "weather-open-meteo";

    /// <summary>The provider's time zone: Bangladesh farms; times come back as farm-local wall clock.</summary>
    public const string TimeZone = "Asia/Dhaka";

    private const string CurrentFields =
        "temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_gusts_10m";

    private const string HourlyFields = "temperature_2m,relative_humidity_2m,precipitation_probability";

    private const string DailyFields =
        "weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max," +
        "wind_speed_10m_max,wind_gusts_10m_max,relative_humidity_2m_mean,sunrise,sunset";

    public string Name => "Open-Meteo";

    public async Task<WeatherForecast> GetForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(options.Value, latitude, longitude);
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new WeatherUnavailableException($"Open-Meteo answered {(int)response.StatusCode}.");

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return OpenMeteoParser.Parse(json);
        }
        catch (WeatherUnavailableException)
        {
            throw;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WeatherUnavailableException("Open-Meteo timed out.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or Polly.CircuitBreaker.BrokenCircuitException
                                       or Polly.Timeout.TimeoutRejectedException or KeyNotFoundException or InvalidOperationException)
        {
            throw new WeatherUnavailableException("Open-Meteo could not be read.", ex);
        }
    }

    /// <summary>The request URL: the configured endpoint, the point and the fields; the key only when set.</summary>
    public static Uri BuildUrl(WeatherOptions settings, double latitude, double longitude)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl) ? "https://api.open-meteo.com/v1/forecast" : settings.BaseUrl.Trim();
        var query = string.Create(CultureInfo.InvariantCulture,
            $"latitude={latitude:0.00}&longitude={longitude:0.00}&current={CurrentFields}&hourly={HourlyFields}&daily={DailyFields}" +
            $"&timezone={Uri.EscapeDataString(TimeZone)}&forecast_days=7&wind_speed_unit=kmh&precipitation_unit=mm");
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            query += "&apikey=" + Uri.EscapeDataString(settings.ApiKey.Trim());
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return new Uri(baseUrl + separator + query);
    }
}

/// <summary>Reads Open-Meteo's JSON (parallel arrays per variable) into <see cref="WeatherForecast"/>.</summary>
public static class OpenMeteoParser
{
    private static readonly string[] TimeFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    public static WeatherForecast Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.True)
            throw new WeatherUnavailableException("Open-Meteo returned an error.");

        var current = root.GetProperty("current");
        var now = new WeatherNow(
            Time(current.GetProperty("time")),
            Number(current, "temperature_2m") ?? throw new FormatException("current.temperature_2m missing"),
            Number(current, "relative_humidity_2m") ?? 0,
            Number(current, "apparent_temperature") ?? Number(current, "temperature_2m") ?? 0,
            Number(current, "precipitation") ?? 0,
            (int)(Number(current, "weather_code") ?? 0),
            Number(current, "wind_speed_10m") ?? 0,
            Number(current, "wind_gusts_10m") ?? Number(current, "wind_speed_10m") ?? 0);

        var hourly = new List<WeatherHour>();
        if (root.TryGetProperty("hourly", out var h) && h.ValueKind == JsonValueKind.Object)
        {
            var times = h.GetProperty("time");
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                var temperature = At(h, "temperature_2m", i);
                if (temperature is null) continue; // the provider has no value for this hour
                hourly.Add(new WeatherHour(Time(times[i]), temperature.Value, At(h, "relative_humidity_2m", i) ?? 0,
                    At(h, "precipitation_probability", i) is { } p ? (int)Math.Round(p) : null));
            }
        }

        var daily = new List<WeatherDay>();
        if (root.TryGetProperty("daily", out var d) && d.ValueKind == JsonValueKind.Object)
        {
            var dates = d.GetProperty("time");
            for (var i = 0; i < dates.GetArrayLength(); i++)
            {
                var max = At(d, "temperature_2m_max", i);
                var min = At(d, "temperature_2m_min", i);
                if (max is null || min is null) continue;
                daily.Add(new WeatherDay(
                    DateOnly.ParseExact(dates[i].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                    max.Value,
                    min.Value,
                    At(d, "precipitation_sum", i) ?? 0,
                    At(d, "precipitation_probability_max", i) is { } p ? (int)Math.Round(p) : null,
                    (int)(At(d, "weather_code", i) ?? 0),
                    At(d, "wind_speed_10m_max", i) ?? 0,
                    At(d, "wind_gusts_10m_max", i),
                    At(d, "relative_humidity_2m_mean", i),
                    TimeAt(d, "sunrise", i),
                    TimeAt(d, "sunset", i)));
            }
        }

        return new WeatherForecast(
            Number(root, "latitude") ?? 0,
            Number(root, "longitude") ?? 0,
            root.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? OpenMeteoWeatherProvider.TimeZone : OpenMeteoWeatherProvider.TimeZone,
            (int)(Number(root, "utc_offset_seconds") ?? 0),
            now,
            hourly,
            daily);
    }

    private static DateTime Time(JsonElement value) =>
        DateTime.ParseExact(value.GetString()!, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static double? Number(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static double? At(JsonElement obj, string name, int index)
    {
        if (!obj.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array || index >= array.GetArrayLength())
            return null;
        var v = array[index];
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    }

    private static DateTime? TimeAt(JsonElement obj, string name, int index)
    {
        if (!obj.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array || index >= array.GetArrayLength())
            return null;
        var v = array[index];
        return v.ValueKind == JsonValueKind.String ? Time(v) : null;
    }
}
