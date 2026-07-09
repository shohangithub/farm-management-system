using FluentValidation.Results;

namespace Farm360.Application.Common.Exceptions;

/// <summary>
/// Thrown when FluentValidation finds errors. HTTP 422 Unprocessable Entity.
/// Constitution §9 (Validation Standards): All validation errors aggregated before throwing.
/// GlobalExceptionMiddleware maps this to RFC 7807 problem details response.
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        Errors = failures
            .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
            .ToDictionary(
                failureGroup => failureGroup.Key,
                failureGroup => failureGroup.ToArray());
    }

    /// <summary>Dictionary of property name → error message array.</summary>
    public IDictionary<string, string[]> Errors { get; }
}

/// <summary>HTTP 404 Not Found. Cross-tenant access also returns 404 (not 403) per security design.</summary>
public sealed class NotFoundException(string name, object key)
    : Exception($"Entity '{name}' with key '{key}' was not found.");

/// <summary>HTTP 403 Forbidden. Only for authorization failures unrelated to tenant isolation.</summary>
public sealed class ForbiddenAccessException(string message = "You are not authorized to perform this action.")
    : Exception(message);

/// <summary>HTTP 409 Conflict. E.g., duplicate animal tag within tenant.</summary>
public sealed class ConflictException(string message)
    : Exception(message);

/// <summary>HTTP 402 Payment Required. Tenant subscription suspended or expired.</summary>
public sealed class TenantSuspendedException(string tenantSlug)
    : Exception($"Tenant '{tenantSlug}' is suspended. Please renew your subscription.");

/// <summary>HTTP 423 Locked. Account locked after failed auth attempts.</summary>
public sealed class AccountLockedException(DateTimeOffset lockoutEnd)
    : Exception($"Account is locked until {lockoutEnd:O}.")
{
    public DateTimeOffset LockoutEnd { get; } = lockoutEnd;
}
