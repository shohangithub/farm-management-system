namespace Farm360.Shared.Attributes;

/// <summary>
/// Marks a property or parameter as containing sensitive/PII data.
/// Constitution §11 (Logging): Serilog destructuring policy skips sensitive-marked properties.
/// Constitution §21 (Security): PII never persisted in raw form in logs.
/// Usage: Apply to phone numbers, OTP values, passwords, financial amounts.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter | AttributeTargets.Field)]
public sealed class SensitiveDataAttribute : Attribute
{
    public SensitiveDataAttribute(string mask = "***REDACTED***")
    {
        Mask = mask;
    }

    /// <summary>The mask value to substitute in logs.</summary>
    public string Mask { get; }
}
