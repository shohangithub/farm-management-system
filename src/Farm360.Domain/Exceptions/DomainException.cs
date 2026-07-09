namespace Farm360.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant is violated.
/// Constitution §10 (Exception Standards): Domain exceptions represent business rule violations.
/// These are caught by GlobalExceptionMiddleware and returned as 422 Unprocessable Entity.
/// NEVER use for infrastructure or technical failures.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
