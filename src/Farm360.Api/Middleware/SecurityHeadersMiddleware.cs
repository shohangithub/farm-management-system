namespace Farm360.Api.Middleware;

/// <summary>
/// Adds baseline security headers to every response. The API only returns JSON and uploaded
/// images, so a locked-down CSP is safe; the dev-only API reference UI (/scalar, /openapi) is
/// excluded because it needs scripts and styles.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    internal const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    /// <summary>Uploaded files: images only, never scripts, never framed, sandboxed if opened directly.</summary>
    internal const string UploadsContentSecurityPolicy = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; sandbox";

    public Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers["X-XSS-Protection"] = "0"; // legacy auditor disabled; CSP is the control

            if (path.StartsWithSegments("/uploads"))
            {
                headers["Content-Security-Policy"] = UploadsContentSecurityPolicy;
            }
            else if (!path.StartsWithSegments("/scalar") && !path.StartsWithSegments("/openapi") &&
                     !path.StartsWithSegments("/hangfire"))
            {
                headers.TryAdd("Content-Security-Policy", ApiContentSecurityPolicy);
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}

/// <summary>
/// Only image files may be served from /uploads. Anything else (HTML, SVG, scripts, unknown
/// extensions) is answered with 404 even if it somehow exists on disk.
/// </summary>
public sealed class UploadsGuardMiddleware(RequestDelegate next)
{
    internal static readonly HashSet<string> AllowedUploadExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/uploads", out var rest))
        {
            var extension = Path.GetExtension(rest.Value ?? string.Empty);
            if (!AllowedUploadExtensions.Contains(extension))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return Task.CompletedTask;
            }

            // Inline display only as an image; never let the browser guess another type.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["Content-Disposition"] = "inline";
                return Task.CompletedTask;
            });
        }

        return next(context);
    }
}
