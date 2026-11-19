using System.Net;
using Farm360.Infrastructure.Caching;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace Farm360.Api.Configuration;

/// <summary>
/// Production hosting concerns that matter once the API runs behind a proxy or as several instances:
/// forwarded headers, a shared Data Protection key ring and optional OpenTelemetry export.
/// </summary>
public static class HostingSetup
{
    // ── Forwarded headers ────────────────────────────────────────────────────

    /// <summary>
    /// Trusts X-Forwarded-For / X-Forwarded-Proto only from the proxies listed in
    /// <c>ForwardedHeaders:KnownProxies</c> (IP addresses) and <c>ForwardedHeaders:KnownNetworks</c> (CIDR),
    /// so <c>RemoteIpAddress</c> — and therefore the per-IP rate limits and audit IPs — is the real client.
    /// Enabled when either list is non-empty or <c>ForwardedHeaders:Enabled</c> is true.
    /// <c>ForwardedHeaders:ForwardLimit</c> (default 1) = number of proxy hops in front of the API.
    /// </summary>
    public static IServiceCollection AddFarm360ForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        var proxies = configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
        var networks = configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
            options.ForwardLimit = configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);

            foreach (var proxy in proxies.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                options.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
            }

            foreach (var network in networks.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network.Trim()));
            }

            // Only accept X-Forwarded-Host values we serve (empty = any; set it when Enabled).
            var allowedHosts = configuration.GetSection("ForwardedHeaders:AllowedHosts").Get<string[]>();
            if (allowedHosts is { Length: > 0 })
            {
                options.AllowedHosts = allowedHosts;
            }
            else
            {
                options.ForwardedHeaders &= ~ForwardedHeaders.XForwardedHost;
            }
        });

        return services;
    }

    public static bool ForwardedHeadersEnabled(IConfiguration configuration) =>
        configuration.GetValue("ForwardedHeaders:Enabled", false)
        || (configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>()?.Length ?? 0) > 0
        || (configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>()?.Length ?? 0) > 0;

    // ── Data Protection ─────────────────────────────────────────────────────

    /// <summary>
    /// Shares the key ring between instances so tokens protected by one instance (Identity
    /// password-reset / confirmation tokens, antiforgery, protected payloads) validate on another,
    /// and survive container restarts.
    /// Priority: <c>DataProtection:KeysPath</c> (file share / volume) → Redis (when the Redis cache is
    /// in use) → the framework default (per-machine; fine for a single non-container instance).
    /// </summary>
    public static IServiceCollection AddFarm360DataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var builder = services.AddDataProtection().SetApplicationName("Farm360");

        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            builder.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }
        else if (CacheConfiguration.UsesRedis(configuration))
        {
            var redis = new Lazy<IConnectionMultiplexer>(
                () => ConnectionMultiplexer.Connect(CacheConfiguration.RedisConnectionString(configuration)!));
            services.Configure<Microsoft.AspNetCore.DataProtection.KeyManagement.KeyManagementOptions>(o =>
                o.XmlRepository = new RedisXmlRepository(() => redis.Value.GetDatabase(), "Farm360:DataProtection-Keys"));
        }

        return services;
    }

    /// <summary>
    /// Key ring stored as a Redis list (same layout as Microsoft.AspNetCore.DataProtection.StackExchangeRedis).
    /// Kept in-house because that package pins a vulnerable Microsoft.AspNetCore.DataProtection 10.0.0
    /// (CVE-2026-40372) and newer versions require bumping every Microsoft.Extensions.* pin.
    /// </summary>
    private sealed class RedisXmlRepository(Func<IDatabase> database, RedisKey key)
        : Microsoft.AspNetCore.DataProtection.Repositories.IXmlRepository
    {
        public IReadOnlyCollection<System.Xml.Linq.XElement> GetAllElements() =>
            database().ListRange(key)
                .Select(v => System.Xml.Linq.XElement.Parse((string)v!))
                .ToList()
                .AsReadOnly();

        public void StoreElement(System.Xml.Linq.XElement element, string friendlyName) =>
            database().ListRightPush(key, element.ToString(System.Xml.Linq.SaveOptions.DisableFormatting));
    }

    // ── OpenTelemetry ───────────────────────────────────────────────────────

    /// <summary>
    /// Exports traces (ASP.NET Core, HttpClient) and metrics (ASP.NET Core, HttpClient, Kestrel, runtime
    /// meters) over OTLP — only when <c>OpenTelemetry:Endpoint</c> is set (e.g. http://otel-collector:4317).
    /// <c>OpenTelemetry:Protocol</c> = grpc (default) | httpprotobuf. <c>OpenTelemetry:ServiceName</c>
    /// defaults to farm360-api. Logs stay on Serilog (they already carry TraceId/SpanId).
    /// </summary>
    public static IServiceCollection AddFarm360OpenTelemetry(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var endpoint = configuration["OpenTelemetry:Endpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return services;
        }

        var protocol = string.Equals(configuration["OpenTelemetry:Protocol"], "httpprotobuf", StringComparison.OrdinalIgnoreCase)
            ? OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf
            : OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
        var serviceName = configuration["OpenTelemetry:ServiceName"] is { Length: > 0 } name ? name : "farm360-api";

        services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceVersion: typeof(HostingSetup).Assembly.GetName().Version?.ToString())
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment.EnvironmentName)]))
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o =>
                    o.Filter = ctx => !ctx.Request.Path.StartsWithSegments(HealthCheckSetup.LegacyPath, StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation())
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel", "System.Runtime"))
            .UseOtlpExporter(protocol, new Uri(endpoint));

        return services;
    }
}
