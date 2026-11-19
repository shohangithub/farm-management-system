using Farm360.Api.Converters;
using Farm360.Api.Endpoints.Farms;
using Farm360.Api.Endpoints.Feeding;
using Farm360.Api.Endpoints.Finance;
using Farm360.Api.Endpoints.Inventory;
using Farm360.Api.Endpoints.Health;
using Farm360.Api.Endpoints.Livestock;
using Farm360.Api.Endpoints.MasterData;
using Farm360.Api.Endpoints.Organizations;
using Farm360.Api.Endpoints.Tenants;
using Farm360.Api.Endpoints.Analytics;
using Farm360.Api.Endpoints;
using Farm360.Api.Endpoints.Auth;
using Farm360.Api.Endpoints.Subscriptions;
using Farm360.Api.Endpoints.Dashboard;
using Farm360.Api.Middleware;
using Farm360.Api.Authorization;
using Farm360.Api.Configuration;
using Farm360.Application.DependencyInjection;
using Farm360.Identity.DependencyInjection;
using Farm360.Infrastructure.DependencyInjection;
using Farm360.Infrastructure.Messaging;
using Farm360.Persistence.DependencyInjection;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

// ══════════════════════════════════════════════════════════════════════════════
// Farm360 AI — Program.cs
// Composition Root: all DI wiring happens here.
// Constitution §2 (Architecture Principles): API is the entry point only.
// Constitution §6 (API Standards): Minimal API routing.
// Middleware pipeline order is CRITICAL — do not reorder without architecture review.
// ══════════════════════════════════════════════════════════════════════════════

// ── Bootstrap Logger (before configuration loads) ────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Farm360 API starting up...");

    var builder = WebApplication.CreateBuilder(args);

    // ── Configuration Strategy ───────────────────────────────────────────────
    // Priority (highest first): Environment Variables > appsettings.{env}.json > appsettings.json
    // Secrets: User Secrets (dev) | AWS Parameter Store (prod)
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        // Re-added AFTER the JSON files so container/orchestrator settings (ConnectionStrings__DefaultConnection,
        // Jwt__Secret, ...) override appsettings*.json. FARM360_-prefixed variables win over unprefixed ones.
        .AddEnvironmentVariables()
        .AddEnvironmentVariables(prefix: "FARM360_");

    if (builder.Environment.IsDevelopment())
    {
        builder.Configuration.AddUserSecrets<Program>(optional: true);
    }

    builder.Configuration.AddCommandLine(args);

    // ── Serilog (replaces default ILogger) ───────────────────────────────────
    builder.Host.UseSerilog((ctx, services, loggerConfig) =>
        Farm360.Infrastructure.Logging.SerilogConfiguration.Configure(
            loggerConfig,
            ctx.Configuration,
            ctx.HostingEnvironment));

    // ══════════════════════════════════════════════════════════════════════════
    // SERVICE REGISTRATION — DI Composition Root
    // Constitution §3: Each layer registers its own services via extension methods.
    // ══════════════════════════════════════════════════════════════════════════

    // Layer 1: Application (CQRS, MediatR pipeline, FluentValidation, AutoMapper)
    builder.Services.AddApplicationServices();

    // Layer 2: Persistence (EF Core, DbContext, Repositories)
    builder.Services.AddPersistenceServices(builder.Configuration);

    // Layer 3: Identity (ASP.NET Identity, JWT, OTP, CurrentUser, TenantService)
    builder.Services.AddIdentityServices(builder.Configuration);

    // Layer 4: Infrastructure (Serilog, Redis, Hangfire, SignalR)
    builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);

    // ── API layer services ───────────────────────────────────────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.SerializerOptions.Converters.Add(new NullableDateOnlyJsonConverter());
        options.SerializerOptions.Converters.Add(new NullableGuidJsonConverter());
    });

    // ── Permission-based Authorization ────────────────────────────────────
    builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionHandler>();
    builder.Services.AddAuthorizationBuilder().AddFarm360Policies(); // PlatformAdmin (sys=true) policy

    // OpenAPI (replaces Swashbuckle in .NET 10 — using Scalar UI)
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "Farm360 AI API",
                Version = "v1",
                Description = "Farm360 AI — Enterprise Livestock Management Platform API",
                Contact = new() { Name = "Farm360 Engineering", Email = "api@farm360.ai" },
            };
            return Task.CompletedTask;
        });
    });

    // Rate limiting: auth endpoints (per IP) + general per-user limit
    builder.Services.AddFarm360RateLimiting(builder.Configuration);

    // ── Production hosting: probes, proxy awareness, shared key ring, telemetry ──
    builder.Services.AddFarm360HealthChecks(builder.Configuration);    // /health/live, /health/ready
    builder.Services.AddFarm360ForwardedHeaders(builder.Configuration); // ForwardedHeaders:KnownProxies/KnownNetworks
    builder.Services.AddFarm360DataProtection(builder.Configuration);   // DataProtection:KeysPath or Redis
    builder.Services.AddFarm360OpenTelemetry(builder.Configuration, builder.Environment); // only if OpenTelemetry:Endpoint

    // CORS — Angular origin
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AngularClient", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:4200"];

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for SignalR + cookie auth
        });
    });

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ══════════════════════════════════════════════════════════════════════════
    // MIDDLEWARE PIPELINE — Order is CRITICAL
    // Constitution §2.5: Middleware ordering per MediatR pipeline specification.
    // ══════════════════════════════════════════════════════════════════════════

    // [0] Forwarded headers — first, so RemoteIpAddress/Scheme are the client's for everything below
    // (rate limiting, logging, HTTPS redirection). Only trusts the configured proxies.
    if (HostingSetup.ForwardedHeadersEnabled(app.Configuration))
    {
        app.UseForwardedHeaders();
    }

    // [1] Exception handling — MUST be outermost (catches all downstream exceptions)
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // [2] Correlation ID — inject before any logging
    app.UseMiddleware<CorrelationIdMiddleware>();

    // [3] Request logging (Serilog) — after correlation ID is set
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} → {StatusCode} ({Elapsed:0.0000}ms) [{TenantId}]";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "");
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
            diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString() ?? "");
            // The completion event is written outside TenantResolutionMiddleware's log scope, so add
            // tenant/user from the token here (empty for anonymous requests).
            diagnosticContext.Set("TenantId", httpContext.User.FindFirst("tenant_id")?.Value ?? "");
            diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "");
        };
        // Probes hit every few seconds: keep them out of the request log unless they fail.
        options.GetLevel = (httpContext, _, ex) =>
            ex is null && httpContext.Response.StatusCode < 500 && httpContext.Request.Path.StartsWithSegments("/health")
                ? Serilog.Events.LogEventLevel.Verbose
                : ex is not null || httpContext.Response.StatusCode >= 500
                    ? Serilog.Events.LogEventLevel.Error
                    : Serilog.Events.LogEventLevel.Information;
    });

    // [4] Security headers (nosniff, frame deny, referrer policy, CSP) — before anything that
    // can write a response, static files included.
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseHsts();
    app.UseHttpsRedirection();

    // Uploaded files are served anonymously from wwwroot/uploads: only image extensions are
    // served (everything else 404s), with the content type taken from the extension and nosniff.
    app.UseMiddleware<UploadsGuardMiddleware>();
    app.UseStaticFiles();

    // [5] CORS
    app.UseCors("AngularClient");

    // [6] OpenAPI + Scalar UI (Development only — the full route map is not published elsewhere)
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Farm360 AI API";
            options.Theme = ScalarTheme.DeepSpace;
        });
    }

    // [7] Authentication — MUST be before Authorization
    app.UseAuthentication();

    // [7b] Rate limiting — after authentication so the general limit can partition per user
    app.UseRateLimiter();

    // [8] Tenant Resolution — MUST be after Authentication (needs JWT claims)
    // F360-MTA-2026-001: Resolves tenant context from JWT → validates status → 402 if suspended
    app.UseMiddleware<TenantResolutionMiddleware>();

    // [9] Authorization
    app.UseAuthorization();

    // [9b] Per-farm access: any {farmId} in the route/query must be in the caller's tenant and allowed farms
    app.UseMiddleware<FarmAccessMiddleware>();

    // [10] Hangfire Dashboard (dev only, secured)
    if (app.Environment.IsDevelopment())
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()],
        });
    }

    // ── Endpoints ─────────────────────────────────────────────────────────────
    // SignalR Hub
    app.MapHub<FarmNotificationHub>("/hubs/farm-notifications")
        .RequireAuthorization();

    // Health probes: /health/live (process), /health/ready (SQL, Redis, Hangfire storage), /health (= live)
    app.MapFarm360HealthChecks();

    // ── Livestock module ────────────────────────────────────────────────────
    app.MapGroup("/api/v1/livestock").MapLivestockEndpoints();
    app.MapGroup("/api/v1/livestock").MapBreedEndpoints();

    // ── Health module ───────────────────────────────────────────────────────
    app.MapHealthEndpoints();

    // ── Smart Feeding module ────────────────────────────────────────────────
    app.MapFeedingEndpoints();

    // ── Organization module ─────────────────────────────────────────────────
    app.MapOrganizationEndpoints();
    app.MapBranchEndpoints();
    app.MapTenantEndpoints();
    app.MapFarmEndpoints();
    app.MapDashboardEndpoints();
    app.MapReportEndpoints();
    app.MapShedEndpoints();
    app.MapPenEndpoints();
    app.MapMasterDataEndpoints();
    app.MapLocationEndpoints();

    app.MapInventoryEndpoints();
    app.MapGroup("/api/v1/auth").MapAuthEndpoints();
    app.MapGroup("/api/v1/users").MapUsersEndpoints();
    app.MapGroup("/api/v1/subscriptions").MapSubscriptionEndpoints();
    app.MapIntelligenceEndpoints();
    app.MapFinanceEndpoints();
    app.MapAnalyticsEndpoints();

    Log.Information("Farm360 API started. Environment: {Environment}", app.Environment.EnvironmentName);

    // Startup security self-check: CRITICAL log (no crash) for dev-only secrets/sandbox outside Development.
    StartupSecurityChecks.LogFindings(
        app.Configuration,
        app.Environment,
        app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Farm360.SecurityChecks"));

    // ── Run Migrations & Data Seeders ────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        // Migrations (fail fast, Database:MigrateOnStartup), reference-data seeders, optional demo tenant
        // (Seed:DemoData) and optional feed-allocation backfill (Maintenance:RunFeedAllocationBackfillOnStartup).
        await StartupTasks.RunAsync(
            scope.ServiceProvider,
            app.Configuration,
            app.Environment,
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Farm360.Startup"));

        // Register Hangfire Recurring Jobs
        var jobService = scope.ServiceProvider.GetRequiredService<Farm360.Application.Common.Interfaces.IBackgroundJobService>();

        // Cron times are farm-local (Asia/Dhaka, resolved on Windows and Linux; fixed UTC+6 fallback):
        // without it "daily at 00:00" ran at 06:00 in Bangladesh.
        var jobTimeZone = Farm360.Application.Common.Time.FarmTimeZones.Default;

       var dailyCron = app.Environment.IsDevelopment() ? Hangfire.Cron.Minutely() : Hangfire.Cron.Daily(0, 0);

        jobService.AddOrUpdateRecurring<MediatR.ISender>(
            "daily-feeding-entries",
            sender => sender.Send(new Farm360.Application.Feeding.Jobs.CreateDailyFeedingEntriesCommand(), CancellationToken.None),
            dailyCron, jobTimeZone); // Runs minutely in dev, at 12:00 AM otherwise

        jobService.AddOrUpdateRecurring<MediatR.ISender>(
            "daily-consumable-entries",
            sender => sender.Send(new Farm360.Application.Inventory.Jobs.CreateDailyConsumableEntriesAcrossTenantsCommand(), CancellationToken.None),
            dailyCron, jobTimeZone);

        jobService.AddOrUpdateRecurring<MediatR.ISender>(
            "close-feeding-cycles",
            sender => sender.Send(new Farm360.Application.Feeding.Jobs.CloseFeedingCycleCommand(), CancellationToken.None),
            Hangfire.Cron.Daily(23, 59), jobTimeZone); // Runs at 11:59 PM

        jobService.AddOrUpdateRecurring<MediatR.ISender>(
            "allocate-overhead-monthly",
            sender => sender.Send(new Farm360.Application.Finance.Jobs.AllocateOverheadAcrossTenantsCommand(), CancellationToken.None),
            Hangfire.Cron.Monthly(3, 2, 0), jobTimeZone); // 3rd of each month at 2:00 AM, covers the previous calendar month

        jobService.AddOrUpdateRecurring<MediatR.ISender>(
            "refresh-tenant-subscription-statuses",
            sender => sender.Send(new Farm360.Application.Tenants.Jobs.RefreshTenantSubscriptionStatusesCommand(), CancellationToken.None),
            Hangfire.Cron.Daily(1, 0), jobTimeZone); // 1:00 AM daily -- proactively suspends tenants whose trial/subscription lapsed
    }

    await app.RunAsync();
}
catch (HostAbortedException)
{
    // EF Core design-time tools (dotnet ef migrations ...) stop the host this way on purpose
    // once they have the DbContext -- not a crash, so don't log it as one.
    throw;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Farm360 API terminated unexpectedly.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

// Required for integration tests (IApiFactory)
public partial class Program { }
