namespace Farm360.Shared.Primitives;

/// <summary>
/// Represents a domain or application error with a code and message.
/// Constitution §10: All errors identified by code. Never use raw strings for error identification.
/// </summary>
public sealed record Error(string Code, string Message)
{
    /// <summary>Sentinel: represents no error (used in successful results).</summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>Sentinel: null/missing value error.</summary>
    public static readonly Error NullValue = new("General.NullValue", "A required value was null.");

    // ── General ─────────────────────────────────────────────────────────────
    public static readonly Error Unauthorized    = new("General.Unauthorized",    "You are not authorized to perform this action.");
    public static readonly Error Forbidden       = new("General.Forbidden",       "Access to this resource is forbidden.");
    public static readonly Error NotFound        = new("General.NotFound",        "The requested resource was not found.");
    public static readonly Error Conflict        = new("General.Conflict",        "A conflict occurred with the current state.");
    public static readonly Error ValidationError = new("General.ValidationError", "One or more validation errors occurred.");
    public static readonly Error ServerError     = new("General.ServerError",     "An unexpected server error occurred.");

    // ── Tenant ──────────────────────────────────────────────────────────────
    public static readonly Error TenantNotFound   = new("Tenant.NotFound",   "The specified tenant was not found.");
    public static readonly Error TenantSuspended  = new("Tenant.Suspended",  "This tenant account has been suspended.");
    public static readonly Error TenantNotActive  = new("Tenant.NotActive",  "This tenant account is not active.");

    // ── Auth ─────────────────────────────────────────────────────────────────
    public static readonly Error InvalidCredentials = new("Auth.InvalidCredentials", "The provided credentials are invalid.");
    public static readonly Error AccountLocked      = new("Auth.AccountLocked",      "This account has been temporarily locked.");
    public static readonly Error TokenExpired       = new("Auth.TokenExpired",       "The authentication token has expired.");
    public static readonly Error InvalidOtp         = new("Auth.InvalidOtp",         "The OTP code is incorrect or has expired.");
    public static readonly Error OtpMaxAttempts     = new("Auth.OtpMaxAttempts",     "Maximum OTP attempts exceeded. Try again later.");

    /// <summary>Factory for dynamic not-found errors with entity context.</summary>
    public static Error NotFoundFor(string entityName, object id) =>
        new($"{entityName}.NotFound", $"{entityName} with id '{id}' was not found.");

    /// <summary>Factory for dynamic conflict errors.</summary>
    public static Error ConflictFor(string entityName, string field) =>
        new($"{entityName}.Conflict.{field}", $"A {entityName} with this {field} already exists.");

    public override string ToString() => $"[{Code}] {Message}";
}
