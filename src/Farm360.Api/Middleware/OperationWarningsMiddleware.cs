using System.Text.Json;
using Farm360.Api.Localization;
using Farm360.Application.Common.Interfaces;

namespace Farm360.Api.Middleware;

/// <summary>
/// Sends the request's non-blocking warnings (see <see cref="IOperationWarnings"/>) to the client in
/// the <c>X-Farm360-Warnings</c> header: a URI-escaped JSON array of messages in the request's
/// language. The request itself still succeeds — a warning never turns into an error.
/// </summary>
public sealed class OperationWarningsMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Farm360-Warnings";

    public async Task InvokeAsync(HttpContext context, IOperationWarnings warnings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(warnings);

        context.Response.OnStarting(() =>
        {
            if (warnings.Messages.Count > 0 && context.Response.StatusCode < 400)
            {
                var messages = warnings.Messages.Select(m => ProblemDetailsLocalizer.Text(context, m)).ToArray();
                context.Response.Headers[HeaderName] = Uri.EscapeDataString(JsonSerializer.Serialize(messages));
            }

            return Task.CompletedTask;
        });

        await next(context);
    }
}
