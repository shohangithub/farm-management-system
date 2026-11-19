using System.Text.Json;
using Farm360.Infrastructure.Caching;
using Farm360.Persistence.Context;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Farm360.Api.Configuration;

/// <summary>
/// Liveness / readiness probes.
/// <list type="bullet">
///   <item><c>/health/live</c> — the process is up and serving requests. No dependencies are touched,
///   so an orchestrator never restarts the API because SQL Server or Redis blipped.</item>
///   <item><c>/health/ready</c> — SQL Server reachable with no pending migrations, Redis reachable (only when
///   the Redis cache is in use) and Hangfire storage reachable. A load balancer should route traffic only
///   to instances that report ready.</item>
///   <item><c>/health</c> — kept as an alias of <c>/health/live</c> for existing uptime monitors.</item>
/// </list>
/// All three are anonymous (they expose only check names, status and timings — never exception
/// details) and are exempt from rate limiting.
/// </summary>
public static class HealthCheckSetup
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string LegacyPath = "/health";

    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    public static IServiceCollection AddFarm360HealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        var checks = services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Process is running."), tags: [LiveTag])
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(10))
            .AddCheck<HangfireStorageHealthCheck>("hangfire-storage", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(10));

        if (CacheConfiguration.UsesRedis(configuration))
        {
            checks.AddCheck<RedisHealthCheck>("redis", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5));
        }

        return services;
    }

    public static IEndpointRouteBuilder MapFarm360HealthChecks(this IEndpointRouteBuilder app)
    {
        var live = new HealthCheckOptions { Predicate = c => c.Tags.Contains(LiveTag), ResponseWriter = WriteJsonAsync };
        var ready = new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag), ResponseWriter = WriteJsonAsync };

        app.MapHealthChecks(LivePath, live).AllowAnonymous().DisableRateLimiting().WithTags("Health");
        app.MapHealthChecks(LegacyPath, live).AllowAnonymous().DisableRateLimiting().WithTags("Health");
        app.MapHealthChecks(ReadyPath, ready).AllowAnonymous().DisableRateLimiting().WithTags("Health");

        return app;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    /// <summary>JSON body: overall status, total duration and one entry per check (no exception text).</summary>
    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store, no-cache";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                description = e.Value.Description,
                data = e.Value.Data.Count == 0 ? null : e.Value.Data,
            }),
        };

        return JsonSerializer.SerializeAsync(context.Response.Body, payload, _jsonOptions, context.RequestAborted);
    }
}

/// <summary>SQL Server reachable and the schema is current (no pending EF migrations).</summary>
internal sealed class SqlServerHealthCheck(ApplicationDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "Cannot connect to SQL Server.");
            }

            var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                // The binary expects a newer schema than the database has: run the migration bundle.
                return new HealthCheckResult(
                    context.Registration.FailureStatus,
                    $"{pending.Count} pending migration(s); run the EF migrations bundle before routing traffic here.",
                    data: new Dictionary<string, object> { ["pendingMigrations"] = pending.Count, ["latestPending"] = pending[^1] });
            }

            return HealthCheckResult.Healthy("SQL Server reachable; schema up to date.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"SQL Server check failed ({ex.GetType().Name}).");
        }
    }
}

/// <summary>Redis (distributed cache) round-trip. Registered only when the Redis cache is in use.</summary>
internal sealed class RedisHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await cache.GetAsync("health:probe", cancellationToken);
            return HealthCheckResult.Healthy("Redis reachable.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, $"Redis check failed ({ex.GetType().Name}).");
        }
    }
}

/// <summary>Hangfire's SQL Server storage answers a trivial read (works whether or not this process runs a Hangfire server).</summary>
internal sealed class HangfireStorageHealthCheck(JobStorage storage) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = storage.GetConnection();
            _ = connection.GetAllEntriesFromHash("farm360:health-probe");
            return Task.FromResult(HealthCheckResult.Healthy("Hangfire storage reachable."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, $"Hangfire storage check failed ({ex.GetType().Name})."));
        }
    }
}
