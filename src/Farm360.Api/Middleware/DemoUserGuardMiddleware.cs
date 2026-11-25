using Farm360.Application.Common.Interfaces;
using Farm360.Application.Demo;

namespace Farm360.Api.Middleware;

/// <summary>
/// The shared demo account is read-only everywhere. The Viewer role already has no write
/// permission, so tenant data is protected by the normal permission checks; this guard also closes
/// the routes that only need "any signed-in user" (change password, sign out everywhere, profile,
/// onboarding, subscription and checkout), which would otherwise let one prospect lock every other
/// one out or reach billing. Every write is refused except an explicit list of read-only POSTs
/// (reports, projections, feed calculators) and signing out.
/// </summary>
public sealed class DemoUserGuardMiddleware(RequestDelegate next)
{
    /// <summary>POST routes that only read or calculate (the body is a query), plus sign-out.</summary>
    private static readonly string[] ReadOnlyPostPrefixes =
    [
        "/api/v1/auth/logout",
        "/api/v1/auth/refresh",
        "/api/v1/auth/demo-login",
        "/hubs/farm-notifications/negotiate", // read-only live notifications
        "/api/v1/reports/",
        "/api/v1/intelligence/projections/calculate",
        "/api/v1/intelligence/projections/solve-break-even",
        "/api/v1/feeding/requirements",
        "/api/v1/feeding/routine",
        "/api/v1/feeding/plans/preview",
        "/api/v1/feeding/sheet/copy-last",
    ];

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        if (DemoConstants.IsDemoViewer(currentUser.UserId) && !IsAllowed(context.Request))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(
                new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Type = "https://farm360.ai/errors/demo-read-only",
                    Title = "Demo is read-only",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = "You're exploring a demo farm with sample data. Nothing can be changed. Start your free trial to manage your own farm.",
                },
                options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json");
            return;
        }

        await next(context);
    }

    internal static bool IsAllowed(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method))
        {
            // Reading is fine, except the payment gateway's redirect-style callbacks.
            return !request.Path.StartsWithSegments("/api/v1/subscriptions/checkout", StringComparison.OrdinalIgnoreCase);
        }

        if (!HttpMethods.IsPost(request.Method))
            return false;

        var path = request.Path.Value ?? string.Empty;
        foreach (var prefix in ReadOnlyPostPrefixes)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            // Report "run" and "export" only; nothing else under /reports is a write anyway.
            if (prefix == "/api/v1/reports/")
                return path.EndsWith("/run", StringComparison.OrdinalIgnoreCase) || path.Contains("/export/", StringComparison.OrdinalIgnoreCase);

            return path.Length == prefix.Length || path[prefix.Length] == '/' || prefix.EndsWith('/');
        }

        return false;
    }
}
