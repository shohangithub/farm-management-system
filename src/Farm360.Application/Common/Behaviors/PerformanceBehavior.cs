using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior: performance monitoring.
/// Constitution §20 (Performance Rules): Log warning for requests exceeding 500ms.
/// Log critical for requests exceeding 2000ms.
/// Runs THIRD in the pipeline.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Threshold for warning log. Constitution §20: 500ms.</summary>
    private const int WarningThresholdMs = 500;

    /// <summary>Threshold for critical alert. Constitution §20: 2000ms.</summary>
    private const int CriticalThresholdMs = 2000;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        // MediatR 12: delegate takes no CancellationToken
        var response = await next();
        stopwatch.Stop();

        var elapsed = stopwatch.ElapsedMilliseconds;
        var requestName = typeof(TRequest).Name;

        if (elapsed >= CriticalThresholdMs)
        {
            if (logger.IsEnabled(LogLevel.Critical))
            {
                logger.LogCritical(
                    "Farm360 SLOW REQUEST CRITICAL: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms). Investigate immediately.",
                    requestName, elapsed, CriticalThresholdMs);
            }
        }
        else if (elapsed >= WarningThresholdMs)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                logger.LogWarning(
                    "Farm360 SLOW REQUEST: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms).",
                    requestName, elapsed, WarningThresholdMs);
            }
        }

        return response;
    }
}
