using Microsoft.EntityFrameworkCore;

namespace Farm360.Api.Middleware;

/// <summary>
/// Assigns or propagates a Correlation ID for every request.
/// Constitution §11 (Logging): CorrelationId is mandatory on all structured log entries.
/// Strategy: Use incoming X-Correlation-Id header if present; generate new GUID if absent.
/// Always echo the correlation ID back in the response header.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string CorrelationIdHeader = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        // Make available to GlobalExceptionMiddleware and all downstream code
        context.Items["CorrelationId"] = correlationId;

        // Push to Serilog LogContext for structured logging
        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            // Echo back in response
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.TryAdd(CorrelationIdHeader, correlationId);
                return Task.CompletedTask;
            });

            await next(context);
        }
    }
}

/// <summary>
/// Tenant Resolution Middleware — resolves and validates tenant per request.
/// F360-MTA-2026-001: Multi-channel tenant resolution strategy.
/// Priority order:
/// [1] JWT tenant_id claim (Authenticated user sessions)
/// [2] X-Tenant-Id HTTP Header (API clients / Mobile apps)
/// [3] Host Subdomain (e.g., {slug}.farm360.ai)
///
/// Flow:
/// [1] Resolve TenantId/Slug from incoming request
/// [2] Lookup tenant in cache (Redis) → fallback to ApplicationDbContext
/// [3] Validate tenant: exists + Active/GracePeriod
/// [4] Set ITenantService for this request scope
/// [5] Push TenantId to Serilog context
///
/// Fail conditions:
///   - TenantId/Slug missing for protected endpoint → 401
///   - Tenant not found                            → 404
///   - Tenant Suspended                            → 402
///   - Anonymous endpoints                          → Skip
/// </summary>
public sealed partial class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        Farm360.Application.Common.Interfaces.ITenantService tenantService,
        Farm360.Application.Common.Interfaces.ICurrentUserService currentUserService,
        Farm360.Application.Common.Interfaces.ICacheService cacheService)
    {
        // Skip anonymous endpoints unless header/subdomain is present for tenant context
        if (!currentUserService.IsAuthenticated && !context.Request.Headers.ContainsKey("X-Tenant-Id"))
        {
            await next(context);
            return;
        }

        Guid? resolvedTenantId = currentUserService.TenantId;
        string? resolvedTenantSlug = null;

        // Strategy 2: X-Tenant-Id Header (if JWT claim not present or for API clients)
        if (!resolvedTenantId.HasValue && context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerVal))
        {
            if (Guid.TryParse(headerVal.FirstOrDefault(), out var parsedHeaderId))
            {
                resolvedTenantId = parsedHeaderId;
            }
            else
            {
                resolvedTenantSlug = headerVal.FirstOrDefault();
            }
        }

        // Strategy 3: Subdomain Resolution (e.g., tenant-slug.farm360.ai)
        if (!resolvedTenantId.HasValue && string.IsNullOrEmpty(resolvedTenantSlug))
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length >= 3 && !parts[0].Equals("www", StringComparison.OrdinalIgnoreCase) && !parts[0].Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                resolvedTenantSlug = parts[0].ToLowerInvariant();
            }
        }

        if (!resolvedTenantId.HasValue && string.IsNullOrEmpty(resolvedTenantSlug))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context not found in token, header, or subdomain." });
            return;
        }

        // Guid.Empty tenant: issued to users with no active tenant membership.
        if (resolvedTenantId == Guid.Empty)
        {
            // Platform administrators (sys=true) operate cross-tenant.
            if (currentUserService.IsSystemUser)
            {
                tenantService.SetTenant(Guid.Empty, "system", "System", "Enterprise", "Active");
                await next(context);
                return;
            }

            // A normal user without an organisation may only reach the routes needed to sign in,
            // see their profile, browse plans and create (onboard) their first organisation.
            // They never receive a tier or access to tenant-scoped modules.
            if (IsAllowedWithoutTenant(context.Request.Path))
            {
                tenantService.SetTenant(Guid.Empty, string.Empty, string.Empty, "None", "NoTenant");
                await next(context);
                return;
            }

            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "No organisation",
                "Your account is not a member of any organisation. Create or join an organisation to continue.");
            return;
        }

        // Resolve tenant from cache or DB
        var cacheKey = resolvedTenantId.HasValue
            ? $"tenant:{resolvedTenantId}:context"
            : $"tenant:slug:{resolvedTenantSlug}:context";

        var tenantEntry = await cacheService.GetAsync<TenantCacheEntry>(cacheKey);

        if (tenantEntry is null)
        {
            var dbContext = context.RequestServices.GetService<Farm360.Persistence.Context.ApplicationDbContext>();
            if (dbContext is not null)
            {
                // Query tenant ignoring query filters (Tenant entity is the root partition)
                var tenantEntity = resolvedTenantId.HasValue
                    ? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                        dbContext.Tenants.IgnoreQueryFilters(), t => t.Id == resolvedTenantId.Value && !t.IsDeleted)
                    : await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                        dbContext.Tenants.IgnoreQueryFilters(), t => t.Slug == resolvedTenantSlug && !t.IsDeleted);

                if (tenantEntity is not null)
                {
                    tenantEntry = new TenantCacheEntry(
                        tenantEntity.Id,
                        tenantEntity.Slug,
                        tenantEntity.Name,
                        tenantEntity.SubscriptionTier.ToString(),
                        tenantEntity.Status.ToString());

                    await cacheService.SetAsync(cacheKey, tenantEntry, TimeSpan.FromMinutes(5));
                }
            }

            // No fallback: an unknown or deleted (cancelled) tenant must never be treated as Active.
        }

        if (tenantEntry is null)
        {
            logger.LogWarning("Tenant {TenantId}/{TenantSlug} not found or deleted -- request rejected.", resolvedTenantId, resolvedTenantSlug);
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Tenant unavailable",
                "Your organisation was not found or is no longer active.");
            return;
        }

        if (tenantEntry.Status == "Cancelled")
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Tenant unavailable",
                "Your organisation was not found or is no longer active.");
            return;
        }

        if (tenantEntry.Status == "Suspended")
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.StartsWith("/api/v1/subscriptions", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/api/v1/billing", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = Localization.ProblemDetailsLocalizer.Text(context, "Your subscription has been suspended. Please renew to continue."),
                    supportUrl = "/settings/billing",
                });
                return;
            }
        }

        // Set tenant context for this request scope
        tenantService.SetTenant(
            tenantEntry.Id,
            tenantEntry.Slug,
            tenantEntry.Name,
            tenantEntry.SubscriptionTier,
            tenantEntry.Status);

        // Push TenantId to Serilog structured context (all subsequent logs include TenantId)
        using (Serilog.Context.LogContext.PushProperty("TenantId", tenantEntry.Id.ToString("N")))
        using (Serilog.Context.LogContext.PushProperty("TenantSlug", tenantEntry.Slug))
        {
            logger.LogDebug("Tenant resolved: {TenantSlug} [{TenantId}]", tenantEntry.Slug, tenantEntry.Id);
            await next(context);
        }
    }
}

/// <summary>Cached tenant context entry.</summary>
public sealed partial class TenantResolutionMiddleware
{
    private static readonly string[] NoTenantAllowedPrefixes =
    [
        "/api/v1/auth",
        "/api/v1/users/profile",
        "/api/v1/tenants/onboard",
        "/api/v1/subscriptions/plans",
        "/api/v1/subscriptions/current",
        "/health",
    ];

    internal static bool IsAllowedWithoutTenant(PathString path) =>
        NoTenantAllowedPrefixes.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));

    private static async Task WriteProblemAsync(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Type = "https://farm360.ai/errors/tenant",
            Title = title,
            Status = status,
            Detail = detail,
        };
        Localization.ProblemDetailsLocalizer.Localize(problem, context);
        await context.Response.WriteAsJsonAsync(
            problem,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json");
    }
}

internal sealed record TenantCacheEntry(
    Guid Id,
    string Slug,
    string Name,
    string SubscriptionTier,
    string Status);
