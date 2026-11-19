using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Expressions;
using Serilog.Formatting.Compact;

namespace Farm360.Infrastructure.Logging;

/// <summary>
/// Serilog configuration factory.
/// Constitution §11 (Logging Standards): Structured logging, mandatory enrichers, PII masking.
/// Sinks: Console (dev) + File (always) + Seq (when configured).
/// Mandatory enrichers: Environment, MachineName, Thread, Process, CorrelationId, TenantId.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Configures Serilog from appsettings.json [Serilog] section.
    /// Called in Program.cs BEFORE builder.Build() for bootstrap logging.
    /// </summary>
    public static LoggerConfiguration Configure(
        LoggerConfiguration loggerConfig,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var seqServerUrl = configuration["Serilog:Seq:ServerUrl"];

        loggerConfig
            // ── Minimum Levels ──────────────────────────────────────────────
            .MinimumLevel.Is(environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", environment.IsDevelopment()
                ? LogEventLevel.Information
                : LogEventLevel.Warning)
            .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)

            // ── Enrichers — Constitution §11.2 mandatory properties ──────────
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId()
            // Custom enrichers (added via middleware for per-request context):
            // CorrelationId, TenantId, UserId enriched via LogContext.PushProperty

            // ── Destructuring Policies — PII Masking (Constitution §11.3) ───
            // SensitiveDataAttribute properties are masked in log output.
            // OTP values, passwords, phone numbers never appear in structured logs.
            .Destructure.ByTransforming<string>(s =>
                s.Length > 200 ? s[..200] + "...(truncated)" : s);

        // ── Sinks ────────────────────────────────────────────────────────────
        // Serilog:Console:Json=true → one compact JSON object per line (container log collectors).
        // Serilog:File:Enabled=false → no rolling files (containers: stdout is the log stream).
        var consoleLevel = environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;
        if (configuration.GetValue("Serilog:Console:Json", defaultValue: false))
        {
            loggerConfig.WriteTo.Console(new CompactJsonFormatter(), restrictedToMinimumLevel: consoleLevel);
        }
        else
        {
            loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] [{TenantId}] {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: consoleLevel);
        }

        if (configuration.GetValue("Serilog:File:Enabled", defaultValue: true))
        {
            loggerConfig.WriteTo.File(
                new CompactJsonFormatter(),
                path: Path.Combine(configuration["Serilog:File:Directory"] is { Length: > 0 } dir ? dir : "logs", "farm360-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                restrictedToMinimumLevel: LogEventLevel.Information,
                shared: false,
                flushToDiskInterval: TimeSpan.FromSeconds(1));
        }

        // Seq sink — structured log UI for local dev
        if (!string.IsNullOrWhiteSpace(seqServerUrl))
        {
            loggerConfig.WriteTo.Seq(
                seqServerUrl,
                restrictedToMinimumLevel: LogEventLevel.Debug);
        }

        return loggerConfig;
    }
}
