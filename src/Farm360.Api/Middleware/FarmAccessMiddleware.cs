using Farm360.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Farm360.Api.Middleware;

/// <summary>
/// Central per-farm access check for HTTP requests: any request whose route (e.g.
/// <c>/api/v1/farms/{farmId}/...</c>) or query string carries a <c>farmId</c> is rejected with 403
/// unless the farm belongs to the caller's tenant and is one of the caller's allowed farms
/// (the JWT <c>farms</c> claim; absent = all farms of the tenant). Runs after authentication,
/// tenant resolution and authorization. Farm ids in request bodies are covered by the MediatR
/// FarmAccessBehavior.
/// </summary>
public sealed class FarmAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser, IFarmAccessService farmAccess)
    {
        if (currentUser.IsAuthenticated && !currentUser.IsSystemUser)
        {
            foreach (var farmId in RequestedFarmIds(context))
            {
                if (!await farmAccess.CanAccessFarmAsync(farmId, context.RequestAborted))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(
                        new ProblemDetails
                        {
                            Type = "https://farm360.ai/errors/forbidden",
                            Title = "Forbidden",
                            Status = StatusCodes.Status403Forbidden,
                            Detail = "You do not have access to this farm.",
                        },
                        options: (System.Text.Json.JsonSerializerOptions?)null,
                        contentType: "application/problem+json");
                    return;
                }
            }
        }

        await next(context);
    }

    internal static IEnumerable<Guid> RequestedFarmIds(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("farmId", out var routeValue)
            && Guid.TryParse(routeValue?.ToString(), out var routeFarmId) && routeFarmId != Guid.Empty)
        {
            yield return routeFarmId;
        }

        foreach (var key in context.Request.Query.Keys)
        {
            if (!string.Equals(key, "farmId", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var value in context.Request.Query[key])
            {
                if (Guid.TryParse(value, out var queryFarmId) && queryFarmId != Guid.Empty)
                {
                    yield return queryFarmId;
                }
            }
        }
    }
}
