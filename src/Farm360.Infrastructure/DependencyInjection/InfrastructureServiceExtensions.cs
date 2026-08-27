using Farm360.Application.Common.Interfaces;
using Farm360.Infrastructure.Caching;
using Farm360.Infrastructure.Logging;
using Farm360.Infrastructure.Messaging;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Farm360.Infrastructure.DependencyInjection;

/// <summary>
/// Infrastructure layer DI registration.
/// Registers: Serilog, Redis, Hangfire, SignalR, Cache, Notification, Background Jobs.
/// Called from Farm360.Api → Program.cs as: builder.Services.AddInfrastructureServices(config, env)
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // ── Serilog (Constitution §11) ─────────────────────────────────────
        services.AddSerilog((_, loggerConfig) =>
            SerilogConfiguration.Configure(loggerConfig, configuration, environment));

        // ── Distributed Cache ──────────────────────────────────────────────
        if (environment.IsProduction())
        {
            // Fallback for Shared Hosting without Redis
            services.AddDistributedMemoryCache();
        }
        else
        {
            var redisConnectionString = configuration.GetConnectionString("Redis")
                ?? throw new InvalidOperationException("'Redis' connection string is not configured.");

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Farm360:";
            });
        }

        services.AddScoped<ICacheService, RedisCacheService>();

        // ── Hangfire Background Jobs ───────────────────────────────────────
        var hangfireConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("'DefaultConnection' connection string not found.");

        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(hangfireConnectionString, new SqlServerStorageOptions
            {
                CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                QueuePollInterval = TimeSpan.Zero,
                UseRecommendedIsolationLevel = true,
                DisableGlobalLocks = true,
            }));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Environment.ProcessorCount * 2;
            options.Queues = ["critical", "default", "low"];
        });

        services.AddScoped<IBackgroundJobService, HangfireBackgroundJobService>();

        // ── SignalR Real-time Notifications ───────────────────────────────
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.MaximumReceiveMessageSize = 32 * 1024; // 32KB
        });

        services.AddScoped<INotificationService, SignalRNotificationService>();

        // ── HTTP Client factory (for external services) ────────────────────
        services.AddHttpClient();

        // ── Messaging services ────────────────────────────────────────────────
        // DEV/STAGING: Log-only stubs — no real messages are sent.
        // PRODUCTION:  Replace with real gateway implementations:
        //   services.AddScoped<ISmsService, TwilioSmsService>();
        //   services.AddScoped<IEmailService, SendGridEmailService>();
        // References: docs/7_Farm360_Solution_Structure.md §Messaging
        services.AddScoped<ISmsService, LoggingSmsService>();
        services.AddScoped<IEmailService, LoggingEmailService>();

        // ── Storage Services ──────────────────────────────────────────────────
        services.AddScoped<IFileStorageService, Farm360.Infrastructure.Storage.LocalFileStorageService>();

        // ── Intelligence Background Services ──────────────────────────────────
        services.AddSingleton<Farm360.Application.Intelligence.Interfaces.IIntelligenceEventChannel, Farm360.Infrastructure.BackgroundServices.Intelligence.IntelligenceEventChannel>();
        services.AddHostedService<Farm360.Infrastructure.BackgroundServices.Intelligence.IntelligenceBackgroundService>();

        return services;
    }
}
