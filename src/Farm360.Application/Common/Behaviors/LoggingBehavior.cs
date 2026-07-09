using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: structured request/response logging.
/// Constitution §11 (Logging Standards): Every request logged with correlation ID, tenant, user, duration.
/// Runs FIRST in the pipeline (outermost wrapper).
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Farm360 Request: {RequestName} started at {StartedAt:O}",
                requestName,
                DateTime.UtcNow);
        }

        try
        {
            // MediatR 12: RequestHandlerDelegate<T> takes NO cancellation token — CT is on the Handle method
            var response = await next();

            stopwatch.Stop();

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation(
                    "Farm360 Request: {RequestName} completed successfully in {ElapsedMs}ms",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            logger.LogError(
                ex,
                "Farm360 Request: {RequestName} failed after {ElapsedMs}ms — {ExceptionType}: {ExceptionMessage}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().Name,
                ex.Message);

            throw;
        }
    }
}
