using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;
using ValidationException = Farm360.Application.Common.Exceptions.ValidationException;

namespace Farm360.Api.Middleware;

/// <summary>
/// Global exception handler middleware.
/// Constitution §10 (Exception Standards): All exceptions mapped to RFC 7807 ProblemDetails.
/// Runs FIRST in the pipeline — catches all unhandled exceptions from downstream.
/// NEVER expose internal exception details to the client in production.
/// CorrelationId always included in error response for support tracing.
/// </summary>
public sealed class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();

        var (statusCode, problemDetails) = exception switch
        {
            // Constitution §9: Validation failures → 422
            ValidationException validationEx => (
                HttpStatusCode.UnprocessableEntity,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/validation",
                    Title = "Validation Failed",
                    Status = (int)HttpStatusCode.UnprocessableEntity,
                    Detail = "One or more validation errors occurred.",
                    Extensions = new Dictionary<string, object?>
                    {
                        ["errors"] = validationEx.Errors,
                        ["correlationId"] = correlationId,
                    },
                }),

            // Constitution §10: Not found → 404
            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/not-found",
                    Title = "Resource Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = notFoundEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Domain rule violation → 422
            DomainException domainEx => (
                HttpStatusCode.UnprocessableEntity,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/domain-rule",
                    Title = "Business Rule Violated",
                    Status = (int)HttpStatusCode.UnprocessableEntity,
                    Detail = domainEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Forbidden → 403
            ForbiddenAccessException forbiddenEx => (
                HttpStatusCode.Forbidden,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/forbidden",
                    Title = "Forbidden",
                    Status = (int)HttpStatusCode.Forbidden,
                    Detail = forbiddenEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Conflict → 409
            ConflictException conflictEx => (
                HttpStatusCode.Conflict,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/conflict",
                    Title = "Conflict",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = conflictEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Tenant suspended → 402
            TenantSuspendedException tenantEx => (
                HttpStatusCode.PaymentRequired,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/subscription-required",
                    Title = "Subscription Required",
                    Status = (int)HttpStatusCode.PaymentRequired,
                    Detail = tenantEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Account locked → 423
            AccountLockedException lockEx => (
                (HttpStatusCode)423,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/account-locked",
                    Title = "Account Temporarily Locked",
                    Status = 423,
                    Detail = lockEx.Message,
                    Extensions = new Dictionary<string, object?>
                    {
                        ["retryAfter"] = lockEx.LockoutEnd,
                        ["correlationId"] = correlationId,
                    },
                }),

            // Authentication failure → 401
            AuthenticationException authEx => (
                HttpStatusCode.Unauthorized,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/unauthorized",
                    Title = "Unauthorized",
                    Status = (int)HttpStatusCode.Unauthorized,
                    Detail = authEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Unauthorized cross-tenant access → 404 (Security: do not reveal resource existence)
            UnauthorizedAccessException => (
                HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/not-found",
                    Title = "Resource Not Found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = "The requested resource was not found.",
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Argument exception (often thrown by domain guards) → 400
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/bad-request",
                    Title = "Bad Request",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = argEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Invalid operation (often thrown by domain rules) → 400
            InvalidOperationException invEx => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/bad-request",
                    Title = "Bad Request",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = invEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Bad HTTP Request (e.g. JSON binding failure, missing required fields) → 400
            BadHttpRequestException badReqEx => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/bad-request",
                    Title = "Bad Request",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = badReqEx.Message,
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // Database constraint violation / unique key conflict → 409 Conflict
            Microsoft.EntityFrameworkCore.DbUpdateException dbUpdateEx when dbUpdateEx.InnerException != null &&
                (dbUpdateEx.InnerException.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
                 dbUpdateEx.InnerException.Message.Contains("unique index", StringComparison.OrdinalIgnoreCase) ||
                 dbUpdateEx.InnerException.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) ||
                 dbUpdateEx.InnerException.Message.Contains("FOREIGN KEY constraint", StringComparison.OrdinalIgnoreCase)) => (
                HttpStatusCode.Conflict,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/database-conflict",
                    Title = "Database Conflict Error",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = "A record with the same unique identifier, tag, or key already exists in the system.",
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // General Database update error → 409 Conflict
            Microsoft.EntityFrameworkCore.DbUpdateException dbEx => (
                HttpStatusCode.Conflict,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/database-conflict",
                    Title = "Database Conflict Error",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = "A database constraint conflict occurred while saving changes.",
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),

            // All other exceptions → 500 (no detail in production)
            _ => (
                HttpStatusCode.InternalServerError,
                new ProblemDetails
                {
                    Type = "https://farm360.ai/errors/server-error",
                    Title = "Server Error",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = "An unexpected error occurred. Please try again later.",
                    Extensions = new Dictionary<string, object?> { ["correlationId"] = correlationId },
                }),
        };

        // Log with appropriate severity
        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception,
                "Unhandled exception [{CorrelationId}] {ExceptionType}: {ExceptionMessage}",
                correlationId, exception.GetType().Name, exception.Message);
        }
        else
        {
            logger.LogWarning(exception,
                "Handled exception [{CorrelationId}] {ExceptionType}: {ExceptionMessage}",
                correlationId, exception.GetType().Name, exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}
