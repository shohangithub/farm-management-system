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
/// F360-MTA-2026-001: Tenant resolved from JWT tenant_id claim after authentication.
/// Pipeline position: AFTER Authentication (needs valid JWT) BEFORE Authorization.
///
/// Flow:
/// [1] Extract TenantId from JWT claim
/// [2] Lookup tenant in cache (Redis) → fallback to DB
/// [3] Validate tenant: exists + Active/GracePeriod
/// [4] Set ITenantService for this request scope
/// [5] Push TenantId to Serilog context
///
/// Fail conditions:
///   - TenantId missing from JWT         → 401
///   - Tenant not found                  → 404
///   - Tenant Suspended                  → 402
///   - Anonymous endpoints               → Skip (AllowAnonymous)
/// </summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        Farm360.Application.Common.Interfaces.ITenantService tenantService,
        Farm360.Application.Common.Interfaces.ICurrentUserService currentUserService,
        Farm360.Application.Common.Interfaces.ICacheService cacheService)
    {
        // Skip anonymous endpoints
        if (!currentUserService.IsAuthenticated)
        {
            await next(context);
            return;
        }

        var tenantId = currentUserService.TenantId;

        // TenantId missing from JWT — invalid token structure
        if (!tenantId.HasValue)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Tenant context not found in token." });
            return;
        }

        // Resolve tenant from cache (F360-MTA-2026-001: tenant data cached per tenant)
        var cacheKey = $"tenant:{tenantId}:context";
        var cached = await cacheService.GetAsync<TenantCacheEntry>(cacheKey);

        TenantCacheEntry? tenantEntry;

        if (cached is not null)
        {
            tenantEntry = cached;
        }
        else
        {
            // TODO: resolve from DB when Tenant entity is implemented
            // tenantEntry = await dbContext.Tenants.FindAsync(tenantId);
            // For scaffolding — placeholder:
            tenantEntry = null;
        }

        if (tenantEntry is null)
        {
            // Tenant not found → 404 (security: do not reveal existence)
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Resource not found." });
            return;
        }

        if (tenantEntry.Status == "Suspended")
        {
            context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Your subscription has been suspended. Please renew to continue.",
                supportUrl = "https://farm360.ai/billing",
            });
            return;
        }

        // Set tenant context for this request scope
        tenantService.SetTenant(
            tenantId.Value,
            tenantEntry.Slug,
            tenantEntry.Name,
            tenantEntry.SubscriptionTier,
            tenantEntry.Status);

        // Push TenantId to Serilog structured context (all subsequent logs include TenantId)
        using (Serilog.Context.LogContext.PushProperty("TenantId", tenantId.Value.ToString("N")))
        using (Serilog.Context.LogContext.PushProperty("TenantSlug", tenantEntry.Slug))
        {
            logger.LogDebug("Tenant resolved: {TenantSlug} [{TenantId}]", tenantEntry.Slug, tenantId);
            await next(context);
        }
    }
}

/// <summary>Cached tenant context entry.</summary>
internal sealed record TenantCacheEntry(
    Guid Id,
    string Slug,
    string Name,
    string SubscriptionTier,
    string Status);
