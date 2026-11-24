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
        // Redis whenever it is configured (required to run more than one API instance);
        // in-process memory otherwise (single instance / shared hosting). See CacheConfiguration.
        if (CacheConfiguration.UsesRedis(configuration))
        {
            var redisConnectionString = CacheConfiguration.RedisConnectionString(configuration);
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Farm360:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
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

        // Hangfire:RunServer (default true) — set false on API replicas when a dedicated worker
        // instance processes jobs. Jobs can still be enqueued/scheduled from any instance.
        // Hangfire:WorkerCount (default ProcessorCount * 2, capped at 20).
        if (configuration.GetValue("Hangfire:RunServer", defaultValue: true))
        {
            var workerCount = configuration.GetValue("Hangfire:WorkerCount", 0);
            services.AddHangfireServer(options =>
            {
                options.WorkerCount = workerCount > 0 ? workerCount : Math.Min(Environment.ProcessorCount * 2, 20);
                options.Queues = ["critical", "default", "low"];
                options.ServerName = $"{Environment.MachineName}:{Environment.ProcessId}";
            });
        }

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

        // ── Payment gateway (subscription checkout) ────────────────────────────
        services.Configure<Farm360.Infrastructure.Payments.SslCommerzOptions>(
            configuration.GetSection(Farm360.Infrastructure.Payments.SslCommerzOptions.SectionName));
        services.AddScoped<Farm360.Application.Common.Interfaces.IPaymentGatewayService,
            Farm360.Infrastructure.Payments.SslCommerzGatewayService>();

        // ── Messaging services ────────────────────────────────────────────────
        // Sms:Provider = Log | BulkSmsBd, Email:Provider = Log | Smtp. "Log" sends nothing (development).
        services.Configure<SmsOptions>(configuration.GetSection(SmsOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.AddHttpClient(BulkSmsBdSmsService.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(15));

        var smsProvider = configuration[$"{SmsOptions.SectionName}:Provider"] ?? "Log";
        if (smsProvider.Equals("BulkSmsBd", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<ISmsService, BulkSmsBdSmsService>();
        else
            services.AddScoped<ISmsService, LoggingSmsService>();

        var emailProvider = configuration[$"{EmailOptions.SectionName}:Provider"] ?? "Log";
        if (emailProvider.Equals("Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IEmailService, SmtpEmailService>();
        else
            services.AddScoped<IEmailService, LoggingEmailService>();

        // ── Storage Services ──────────────────────────────────────────────────
        services.AddScoped<IFileStorageService, Farm360.Infrastructure.Storage.LocalFileStorageService>();

        // ── Intelligence Background Services ──────────────────────────────────
        services.AddSingleton<Farm360.Application.Intelligence.Interfaces.IIntelligenceEventChannel, Farm360.Infrastructure.BackgroundServices.Intelligence.IntelligenceEventChannel>();
        services.AddHostedService<Farm360.Infrastructure.BackgroundServices.Intelligence.IntelligenceBackgroundService>();

        // ── Business rules (docs/33): the farm's configurable assumptions ─────
        services.Configure<Farm360.Domain.BusinessRules.FarmBusinessRules>(
            configuration.GetSection(Farm360.Domain.BusinessRules.FarmBusinessRules.SectionName));
        // Per tenant (Settings → Farm rules) over the config defaults. The cache is a singleton holding
        // no scoped state; the provider is scoped because it reads the request tenant (ITenantService).
        services.AddMemoryCache();
        services.AddSingleton<Farm360.Infrastructure.BusinessRules.FarmBusinessRulesCache>();
        services.AddScoped<Farm360.Application.Common.Interfaces.IFarmBusinessRulesProvider,
            Farm360.Infrastructure.BusinessRules.FarmBusinessRulesProvider>();

        // ── Reporting renderers (docs/32) ─────────────────────────────────────
        // One per output format; ReportExecutionService picks by IReportRenderer.Format.
        var reportingRules = configuration
            .GetSection(Farm360.Domain.BusinessRules.FarmBusinessRules.SectionName)
            .Get<Farm360.Domain.BusinessRules.FarmBusinessRules>() ?? new Farm360.Domain.BusinessRules.FarmBusinessRules();
        Farm360.Infrastructure.Reporting.ReportFonts.EnsureRegistered(reportingRules.PdfLicence, reportingRules.PdfLicenceKey);
        services.AddSingleton<Farm360.Application.Reporting.Abstractions.IReportRenderer, Farm360.Infrastructure.Reporting.Pdf.QuestPdfReportRenderer>();
        services.AddSingleton<Farm360.Application.Reporting.Abstractions.IReportRenderer, Farm360.Infrastructure.Reporting.Excel.ClosedXmlReportRenderer>();
        services.AddSingleton<Farm360.Application.Reporting.Abstractions.IReportRenderer, Farm360.Infrastructure.Reporting.Csv.CsvReportRenderer>();

        // Customer documents (sale invoice, payment receipt, purchase order) share the report letterhead.
        services.AddSingleton<Farm360.Application.Documents.ICustomerDocumentRenderer, Farm360.Infrastructure.Documents.QuestPdfCustomerDocumentRenderer>();

        return services;
    }
}
