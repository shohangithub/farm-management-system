using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Farm360.Api.Configuration;

/// <summary>
/// ASP.NET Core rate limiting.
/// <list type="bullet">
///   <item><see cref="AuthPolicy"/>: login / refresh (and future OTP) — fixed window per client IP.</item>
///   <item><see cref="RegistrationPolicy"/>: account + trial-tenant creation — much stricter, per IP.</item>
///   <item>Global limiter: sliding window per authenticated user (per IP when anonymous).</item>
/// </list>
/// Behind a reverse proxy, configure ForwardedHeaders so RemoteIpAddress is the real client IP.
/// Limits can be tuned via the <c>RateLimiting</c> configuration section.
/// </summary>
public static class RateLimitingSetup
{
    public const string AuthPolicy = "auth";
    public const string RegistrationPolicy = "registration";

    public static IServiceCollection AddFarm360RateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var authPermitLimit = configuration.GetValue("RateLimiting:Auth:PermitPerMinute", 10);
        var registerPermitLimit = configuration.GetValue("RateLimiting:Register:PermitPerHour", 5);
        var userPermitLimit = configuration.GetValue("RateLimiting:User:PermitPerMinute", 600);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Type = "https://farm360.ai/errors/rate-limited",
                        Title = "Too Many Requests",
                        Status = StatusCodes.Status429TooManyRequests,
                        Detail = "Too many requests. Please wait a moment and try again.",
                    },
                    options: (System.Text.Json.JsonSerializerOptions?)null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };

            options.AddPolicy(AuthPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                $"auth:{ClientIp(http)}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = authPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.AddPolicy(RegistrationPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                $"register:{ClientIp(http)}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registerPermitLimit,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));

            // General per-user limit. Health checks and payment-gateway callbacks are exempt.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var path = http.Request.Path;
                if (path.StartsWithSegments("/health") || path.StartsWithSegments("/api/v1/subscriptions/checkout"))
                {
                    return RateLimitPartition.GetNoLimiter("exempt");
                }

                var userId = http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                // Every prospect shares the demo account: limit them per IP, not as one user.
                var isDemo = Guid.TryParse(userId, out var parsedUserId) && Farm360.Application.Demo.DemoConstants.IsDemoViewer(parsedUserId);
                var key = userId is null ? $"ip:{ClientIp(http)}"
                    : isDemo ? $"demo:{ClientIp(http)}"
                    : $"user:{userId}";

                return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = userPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    private static string ClientIp(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
