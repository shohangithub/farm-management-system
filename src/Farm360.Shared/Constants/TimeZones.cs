namespace Farm360.Shared.Constants;

/// <summary>
/// Bangladesh-specific time zone constants.
/// Constitution §12 (Database Standards): All DateTimes stored as UTC.
/// Displayed in BST (UTC+6) in the UI.
/// F360-CONST-2026-001 §19 (UI Standards): Date format DD/MM/YYYY Bangladesh standard.
/// </summary>
public static class TimeZones
{
    /// <summary>Bangladesh Standard Time (BST) — UTC+6. No DST.</summary>
    public const string BangladeshStandardTime = "Bangladesh Standard Time";

    /// <summary>IANA identifier for BST.</summary>
    public const string BangladeshIana = "Asia/Dhaka";

    /// <summary>UTC offset for BST: +06:00</summary>
    public static readonly TimeSpan BstOffset = TimeSpan.FromHours(6);

    /// <summary>Converts UTC DateTime to Bangladesh Standard Time.</summary>
    public static DateTime ToBst(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("DateTime must be UTC.", nameof(utcDateTime));
        }

        var bstZone = TimeZoneInfo.FindSystemTimeZoneById(BangladeshStandardTime);
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, bstZone);
    }
}

/// <summary>Farm360 date format constants — Bangladesh Standard.</summary>
public static class DateFormats
{
    /// <summary>Standard date display format: 07/07/2026</summary>
    public const string Display = "dd/MM/yyyy";

    /// <summary>Date with time display: 07/07/2026 10:30 AM</summary>
    public const string DisplayWithTime = "dd/MM/yyyy hh:mm tt";

    /// <summary>ISO 8601 for API responses.</summary>
    public const string ApiFormat = "yyyy-MM-ddTHH:mm:ssZ";

    /// <summary>Month/Year: July 2026</summary>
    public const string MonthYear = "MMMM yyyy";

    /// <summary>Short month: Jul 2026</summary>
    public const string ShortMonthYear = "MMM yyyy";
}
