using Farm360.Api.Converters;
using Farm360.Api.Endpoints.Farms;
using Farm360.Api.Endpoints.Feeding;
using Farm360.Api.Endpoints.Health;
using Farm360.Api.Endpoints.Livestock;
using Farm360.Api.Endpoints.MasterData;
using Farm360.Api.Endpoints.Organizations;
using Farm360.Api.Endpoints.Tenants;
using Farm360.Api.Endpoints.Auth;
using Farm360.Api.Middleware;
using Farm360.Api.Authorization;
using Farm360.Application.DependencyInjection;
using Farm360.Identity.DependencyInjection;
using Farm360.Infrastructure.DependencyInjection;
using Farm360.Infrastructure.Messaging;
using Farm360.Persistence.DependencyInjection;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        .AddEnvironmentVariables(prefix: "FARM360_")
        .AddUserSecrets<Program>(optional: true); // Dev only

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
        };
    });

    // [4] Security headers
    app.UseHsts();
    app.UseHttpsRedirection();
    
    // Allow serving static files (e.g. uploads from wwwroot)
    app.UseStaticFiles();

    app.Use(async (context, next) =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
        context.Response.Headers.TryAdd("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    // [5] CORS
    app.UseCors("AngularClient");

    // [6] OpenAPI + Scalar UI (dev only)
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

    // [8] Tenant Resolution — MUST be after Authentication (needs JWT claims)
    // F360-MTA-2026-001: Resolves tenant context from JWT → validates status → 402 if suspended
    app.UseMiddleware<TenantResolutionMiddleware>();

    // [9] Authorization
    app.UseAuthorization();

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

    // Health check
    app.MapGet("/health", () => Results.Ok(new
    {
        Status = "Healthy",
        Timestamp = DateTime.UtcNow,
        Version = "1.0.0",
    })).AllowAnonymous().WithTags("Health");

    // ── Livestock module ────────────────────────────────────────────────────
    app.MapGroup("/api/v1/livestock").MapLivestockEndpoints();

    // ── Health module ───────────────────────────────────────────────────────
    app.MapHealthEndpoints();

    // ── Smart Feeding module ────────────────────────────────────────────────
    app.MapFeedingEndpoints();

    // ── Organization module ─────────────────────────────────────────────────
    app.MapOrganizationEndpoints();
    app.MapBranchEndpoints();
    app.MapTenantEndpoints();
    app.MapFarmEndpoints();
    app.MapShedEndpoints();
    app.MapPenEndpoints();
    app.MapMasterDataEndpoints();
    app.MapLocationEndpoints();

    // app.MapGroup("/api/v1/feeding").MapFeedingEndpoints();
    // app.MapGroup("/api/v1/finance").MapFinanceEndpoints();
    // app.MapGroup("/api/v1/inventory").MapInventoryEndpoints();
    app.MapGroup("/api/v1/auth").MapAuthEndpoints();
    app.MapGroup("/api/v1/users").MapUsersEndpoints();

    Log.Information("Farm360 API started. Environment: {Environment}", app.Environment.EnvironmentName);

    // ── Run Data Seeders ──────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var dataSeeder = scope.ServiceProvider.GetRequiredService<Farm360.Persistence.Seed.DataSeeder>();
        await dataSeeder.SeedAsync();

        var identitySeeder = scope.ServiceProvider.GetRequiredService<Farm360.Identity.Seed.IdentitySeeder>();
        await identitySeeder.SeedAsync();
    }

    await app.RunAsync();
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
