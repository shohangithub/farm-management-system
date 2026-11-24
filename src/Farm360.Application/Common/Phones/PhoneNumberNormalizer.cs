namespace Farm360.Application.Common.Phones;

/// <summary>
/// Canonical form for sign-in phone numbers (the phone is the login ID).
/// Bangladeshi users type the same number as <c>017XXXXXXXX</c>, <c>88017XXXXXXXX</c> or
/// <c>+88017XXXXXXXX</c>; all of them become <c>+88017XXXXXXXX</c>. Other numbers only lose their
/// spaces and dashes.
/// </summary>
public static class PhoneNumberNormalizer
{
    private const string BangladeshCountryCode = "880";

    /// <summary>
    /// Returns the canonical phone number, or <c>null</c> when the input is empty.
    /// <list type="bullet">
    ///   <item>spaces and dashes are removed;</item>
    ///   <item><c>01XXXXXXXXX</c> (11 digits) → <c>+8801XXXXXXXXX</c>;</item>
    ///   <item><c>8801…</c> → <c>+8801…</c>;</item>
    ///   <item>anything else (e.g. <c>+44…</c>) is left as it is.</item>
    /// </list>
    /// </summary>
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var compact = string.Concat(phone.Where(c => !char.IsWhiteSpace(c) && c != '-'));
        if (compact.Length == 0) return null;

        if (compact.Length == 11 && compact.StartsWith("01", StringComparison.Ordinal) && IsAllDigits(compact))
            return "+88" + compact; // 01… already carries the trunk 0 of +880

        if (compact.StartsWith(BangladeshCountryCode + "1", StringComparison.Ordinal) && IsAllDigits(compact))
            return "+" + compact;

        return compact;
    }

    /// <summary>
    /// The values to try, in order, when looking an account up by phone: the canonical form first,
    /// then the input as typed (trimmed), then — for a Bangladeshi mobile — its local
    /// (<c>01…</c>) and no-plus (<c>8801…</c>) spellings, so accounts stored before phones were
    /// normalised can still be found. Distinct; empty when the input is empty.
    /// </summary>
    public static IReadOnlyList<string> LookupCandidates(string? phone)
    {
        var normalized = Normalize(phone);
        if (normalized is null) return [];

        var candidates = new List<string> { normalized, phone!.Trim() };
        if (normalized.Length == 14 && normalized.StartsWith("+8801", StringComparison.Ordinal))
        {
            candidates.Add(normalized[3..]); // 01XXXXXXXXX
            candidates.Add(normalized[1..]); // 8801XXXXXXXXX
        }

        return candidates.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>True when the canonical form is an optional <c>+</c> followed by 6–20 digits.</summary>
    public static bool IsValid(string? phone)
    {
        var normalized = Normalize(phone);
        if (normalized is null) return false;

        var digits = normalized.StartsWith('+') ? normalized[1..] : normalized;
        return digits.Length is >= 6 and <= 20 && IsAllDigits(digits);
    }

    private static bool IsAllDigits(string value) => value.All(char.IsAsciiDigit);
}
