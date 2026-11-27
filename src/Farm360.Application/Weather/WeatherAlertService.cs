using System.Globalization;
using System.Text.Json;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Jobs;
using Farm360.Application.InAppNotifications;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Tenancy;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Weather;

/// <summary>Creates the daily weather bell notification for one farm.</summary>
public interface IWeatherAlertService
{
    /// <summary>
    /// When today or tomorrow brings a storm, severe heat, heavy rain or a cold wave: one notification per
    /// targeted user for the farm and day (running it again the same day creates nothing).
    /// </summary>
    Task<IReadOnlyList<Notification>> GenerateForFarmAsync(Guid tenantId, Farm farm, DateOnly today, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IWeatherAlertService"/>
public sealed class WeatherAlertService(
    IFarmWeatherService weather,
    IHealthReminderRecipients recipients,
    INotificationStore store,
    IUnitOfWork unitOfWork,
    ILogger<WeatherAlertService> logger,
    INotificationService? live = null) : IWeatherAlertService
{
    public const string Category = "weather.alert";
    public const string Link = "/weather";

    public async Task<IReadOnlyList<Notification>> GenerateForFarmAsync(Guid tenantId, Farm farm, DateOnly today, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(farm);
        if (!FarmWeatherService.TryLocation(farm.Latitude, farm.Longitude, out _))
            return [];

        // Same people as the health reminders: Owner, Farm Manager, Vet, Worker with access to the farm;
        // never a Viewer or the demo visitor.
        var userIds = await recipients.GetUserIdsAsync(tenantId, farm.Id, cancellationToken);
        if (userIds.Count == 0)
            return [];

        var key = DedupKey(farm.Id, today);
        var existing = await store.GetExistingKeysAsync(userIds, [key], cancellationToken);
        var pending = userIds.Where(u => !existing.Contains((u, key))).ToList();
        if (pending.Count == 0)
            return []; // already done today: no provider call

        var result = await weather.GetForFarmAsync(farm, cancellationToken);
        var alerts = result.Report?.Alerts ?? [];
        if (!result.Available || alerts.Count == 0)
            return [];

        var (title, body, parameters) = Texts(alerts);
        var created = new List<Notification>();
        foreach (var userId in pending)
        {
            var notification = Notification.CreateReminder(tenantId, userId, farm.Id, NotificationType.Warning, Category,
                title, body, parameters, Link, key);
            store.Add(notification);
            created.Add(notification);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (DatabaseErrors.IsUniqueViolation(ex))
        {
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Weather alert for farm {FarmId} on {Day} was already created by a concurrent run.", farm.Id, today);
            return [];
        }

        if (live is not null)
        {
            foreach (var notification in created)
            {
#pragma warning disable CA1031 // A failed live push must not undo stored alerts; the bell loads them anyway.
                try
                {
                    await live.SendToUserAsync(tenantId, notification.UserId, NotificationDto.LiveEventName,
                        NotificationDto.From(notification), cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Live push of notification {NotificationId} failed.", notification.Id);
                }
#pragma warning restore CA1031
            }
        }

        return created;
    }

    public static string DedupKey(Guid farmId, DateOnly day) => $"weather:{farmId:N}:{day:yyyyMMdd}";

    /// <summary>Title and body keys (the web translates them) for the most dangerous alert, with the count.</summary>
    public static (string Title, string Body, string Params) Texts(IReadOnlyList<WeatherAlertDto> alerts)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        var top = alerts[0];
        var tomorrow = top.Window == WeatherWindows.Tomorrow;
        var body = top.Kind switch
        {
            WeatherAnalyzer.AlertStorm => tomorrow
                ? "Storm risk tomorrow: gusts up to {value} km/h. Secure the roof and keep the animals inside."
                : "Storm risk today: gusts up to {value} km/h. Secure the roof and keep the animals inside.",
            WeatherAnalyzer.AlertHeat => tomorrow
                ? "Severe heat tomorrow (THI up to {value}). Give extra water and shade; no transport or vaccination at midday."
                : "Severe heat today (THI up to {value}). Give extra water and shade; no transport or vaccination at midday.",
            WeatherAnalyzer.AlertRain => tomorrow
                ? "Heavy rain tomorrow (about {value} mm). Keep the shed dry and the feed covered."
                : "Heavy rain today (about {value} mm). Keep the shed dry and the feed covered.",
            _ => tomorrow
                ? "Cold tomorrow: down to {value}°C. Cover calves and weak animals and block cold wind."
                : "Cold today: down to {value}°C. Cover calves and weak animals and block cold wind.",
        };
        var title = alerts.Count == 1 ? "Weather alert" : "Weather alerts ({count})";
        var parameters = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["value"] = top.Value,
            ["count"] = alerts.Count.ToString(CultureInfo.InvariantCulture),
        });
        return (title, body, parameters);
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// Daily weather alerts (Hangfire)
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Hourly job (at :30): for every tenant, each farm with a location whose local clock has reached 6:30 gets
/// that day's weather alert once (see <see cref="IWeatherAlertService"/>).
/// </summary>
public sealed record GenerateWeatherAlertsCommand : IRequest<TenantJobSummary>;

internal sealed class GenerateWeatherAlertsCommandHandler(ITenantJobRunner runner)
    : IRequestHandler<GenerateWeatherAlertsCommand, TenantJobSummary>
{
    public Task<TenantJobSummary> Handle(GenerateWeatherAlertsCommand request, CancellationToken cancellationToken) =>
        runner.ForEachTenantAsync("weather-alerts",
            (scope, ct) => scope.Sender.Send(new GenerateTenantWeatherAlertsCommand(scope.TenantId), ct),
            cancellationToken);
}

/// <summary>One tenant's weather alerts (the ambient tenant must already be set). Returns how many were created.</summary>
public sealed record GenerateTenantWeatherAlertsCommand(Guid TenantId, bool IgnoreAlertTime = false) : IRequest<int>
{
    /// <summary>Farm-local time from which the day's alert is made.</summary>
    public static TimeOnly AlertTime { get; } = new(6, 30);
}

internal sealed class GenerateTenantWeatherAlertsCommandHandler(
    IFarmRepository farms,
    IWeatherAlertService alerts,
    ITenantService tenantService,
    IFarmClock? clock = null) : IRequestHandler<GenerateTenantWeatherAlertsCommand, int>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<int> Handle(GenerateTenantWeatherAlertsCommand request, CancellationToken cancellationToken)
    {
        // The public demo farm is visited anonymously: no alerts.
        if (Farm360.Application.Demo.DemoConstants.IsDemoSlug(tenantService.TenantSlug))
            return 0;

        var created = 0;
        foreach (var farm in await farms.GetAllByTenantAsync(request.TenantId, cancellationToken))
        {
            if (!FarmWeatherService.TryLocation(farm.Latitude, farm.Longitude, out _))
                continue;

            var local = TimeZoneInfo.ConvertTimeFromUtc(_clock.UtcNow, _clock.ZoneFor(request.TenantId, farm.Id));
            if (!request.IgnoreAlertTime && TimeOnly.FromDateTime(local) < GenerateTenantWeatherAlertsCommand.AlertTime)
                continue;

            created += (await alerts.GenerateForFarmAsync(request.TenantId, farm, DateOnly.FromDateTime(local), cancellationToken)).Count;
        }

        return created;
    }
}
