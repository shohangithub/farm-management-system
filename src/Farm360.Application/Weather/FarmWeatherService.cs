using System.Globalization;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farm360.Application.Weather;

/// <summary>A farm's weather: its coordinates → cached provider forecast → cattle analysis.</summary>
public interface IFarmWeatherService
{
    /// <summary>The farm's weather; null when the farm does not exist in the tenant.</summary>
    Task<FarmWeatherDto?> GetAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>The weather for an already-loaded farm (the daily alert job).</summary>
    Task<FarmWeatherDto> GetForFarmAsync(Farm farm, CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads the farm's coordinates, rounds them to 2 decimals (about 1 km: the cache key, and all the
/// provider ever sees), reuses a cached forecast for <see cref="WeatherOptions.CacheMinutes"/>, and never
/// throws for a provider failure: the result then says <c>Available = false</c> ("Weather unavailable").
/// </summary>
public sealed class FarmWeatherService(
    IFarmRepository farms,
    IWeatherProvider provider,
    ICacheService cache,
    IOptions<WeatherOptions> options,
    ILogger<FarmWeatherService> logger,
    IDateTimeService? dateTime = null) : IFarmWeatherService
{
    /// <summary>What is cached per point: the forecast and when it was fetched.</summary>
    public sealed record CachedForecast(WeatherForecast Forecast, DateTime FetchedAtUtc, string Provider);

    public async Task<FarmWeatherDto?> GetAsync(Guid tenantId, Guid farmId, CancellationToken cancellationToken = default)
    {
        var farm = await farms.GetByIdAsync(tenantId, farmId, cancellationToken);
        return farm is null ? null : await GetForFarmAsync(farm, cancellationToken);
    }

    public async Task<FarmWeatherDto> GetForFarmAsync(Farm farm, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(farm);
        if (!TryLocation(farm.Latitude, farm.Longitude, out var location))
            return FarmWeatherDto.MissingLocation(farm.Id, farm.BranchId);

        var settings = options.Value;
        if (!settings.Enabled)
            return FarmWeatherDto.Unavailable(farm.Id, farm.BranchId, location);

        var cached = await LoadForecastAsync(location, settings, cancellationToken);
        if (cached is null)
            return FarmWeatherDto.Unavailable(farm.Id, farm.BranchId, location);

        var utcNow = dateTime?.UtcNow ?? DateTime.UtcNow;
        var localNow = DateTime.SpecifyKind(utcNow.AddSeconds(cached.Forecast.UtcOffsetSeconds), DateTimeKind.Unspecified);
        var report = WeatherAnalyzer.Analyze(cached.Forecast, localNow);
        return new FarmWeatherDto(farm.Id, farm.BranchId, false, true, cached.Provider, location, cached.FetchedAtUtc, report);
    }

    /// <summary>
    /// Valid coordinates, rounded to 2 decimals. Missing, out of range or exactly (0, 0) (an unset
    /// form, in the sea off Africa) count as "no location".
    /// </summary>
    public static bool TryLocation(double? latitude, double? longitude, out WeatherLocationDto location)
    {
        location = new WeatherLocationDto(0, 0);
        if (latitude is not { } lat || longitude is not { } lon) return false;
        if (double.IsNaN(lat) || double.IsNaN(lon) || lat is < -90 or > 90 || lon is < -180 or > 180) return false;
        if (lat == 0 && lon == 0) return false;
        location = new WeatherLocationDto(Math.Round(lat, 2), Math.Round(lon, 2));
        return true;
    }

    public static string CacheKey(string providerName, WeatherLocationDto location) =>
        string.Create(CultureInfo.InvariantCulture,
            $"weather:forecast:{providerName.ToUpperInvariant()}:{location.Latitude:F2}:{location.Longitude:F2}");

    private async Task<CachedForecast?> LoadForecastAsync(WeatherLocationDto location, WeatherOptions settings, CancellationToken cancellationToken)
    {
        var key = CacheKey(provider.Name, location);
        var cached = await cache.GetAsync<CachedForecast>(key, cancellationToken);
        if (cached?.Forecast?.Current is not null)
            return cached;

        try
        {
            var forecast = await provider.GetForecastAsync(location.Latitude, location.Longitude, cancellationToken);
            var fresh = new CachedForecast(forecast, dateTime?.UtcNow ?? DateTime.UtcNow, provider.Name);
            await cache.SetAsync(key, fresh, TimeSpan.FromMinutes(Math.Max(1, settings.CacheMinutes)), cancellationToken);
            return fresh;
        }
        catch (WeatherUnavailableException ex)
        {
            // The coordinates are rounded public map points, not personal data.
            logger.LogWarning(ex, "Weather provider {Provider} failed for {Latitude},{Longitude}.", provider.Name, location.Latitude, location.Longitude);
            return null;
        }
    }
}
