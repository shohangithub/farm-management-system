using System;
using System.Globalization;

namespace Farm360.Application.Reporting.Model;

/// <summary>
/// The single place any Farm360 report turns a value into text. PDF, Excel, CSV and the
/// on-screen viewer all route through here, which is what guarantees they agree.
/// </summary>
public static class ReportValueFormatter
{
    /// <summary>
    /// Reports are printed documents, so formatting is locale-invariant by design: a report
    /// filed in Dhaka and one opened in London must show the same digits.
    /// </summary>
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Bengali digits, indexed 0-9. Used only when the farm opts into Bengali numerals.</summary>
    private static readonly string[] BengaliDigits = ["০", "১", "২", "৩", "৪", "৫", "৬", "৭", "৮", "৯"];

    public static string Format(object? value, ReportColumnType type, int decimals, bool bengaliNumerals = false)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var text = type switch
        {
            ReportColumnType.Text => value.ToString() ?? string.Empty,
            ReportColumnType.Date => FormatDate(value),
            ReportColumnType.DateTime => FormatDateTime(value),
            ReportColumnType.WholeNumber => ToDecimal(value).ToString("N0", Invariant),
            ReportColumnType.Number => ToDecimal(value).ToString(NumericFormat(decimals), Invariant),
            ReportColumnType.Money => ToDecimal(value).ToString(NumericFormat(decimals), Invariant),

            // Stored as a ratio (0.1326), printed as a percentage (13.26) — the "%" sign lives in
            // the column header, not in every cell, so the column stays narrow and scannable.
            ReportColumnType.Percent => (ToDecimal(value) * 100m).ToString(NumericFormat(decimals), Invariant),

            ReportColumnType.Boolean => ToBool(value) ? "Yes" : "No",
            _ => value.ToString() ?? string.Empty,
        };

        return bengaliNumerals ? ToBengaliNumerals(text) : text;
    }

    private static string NumericFormat(int decimals) => decimals <= 0 ? "N0" : "N" + decimals.ToString(Invariant);

    private static string FormatDate(object value) => value switch
    {
        DateOnly d => d.ToString("dd-MMM-yyyy", Invariant),
        DateTime dt => DateOnly.FromDateTime(dt).ToString("dd-MMM-yyyy", Invariant),
        DateTimeOffset dto => DateOnly.FromDateTime(dto.UtcDateTime).ToString("dd-MMM-yyyy", Invariant),
        _ => value.ToString() ?? string.Empty,
    };

    private static string FormatDateTime(object value) => value switch
    {
        DateTime dt => dt.ToString("dd-MMM-yyyy HH:mm", Invariant),
        DateTimeOffset dto => dto.UtcDateTime.ToString("dd-MMM-yyyy HH:mm", Invariant),
        DateOnly d => d.ToString("dd-MMM-yyyy", Invariant),
        _ => value.ToString() ?? string.Empty,
    };

    public static decimal ToDecimal(object? value) => value switch
    {
        null => 0m,
        decimal d => d,
        double db => (decimal)db,
        float f => (decimal)f,
        int i => i,
        long l => l,
        short s => s,
        byte b => b,
        string s when decimal.TryParse(s, NumberStyles.Any, Invariant, out var parsed) => parsed,
        IConvertible c => SafeConvert(c),
        _ => 0m,
    };

    private static decimal SafeConvert(IConvertible c)
    {
        try
        {
            return c.ToDecimal(Invariant);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return 0m;
        }
    }

    private static bool ToBool(object value) => value switch
    {
        bool b => b,
        string s => bool.TryParse(s, out var parsed) && parsed,
        _ => false,
    };

    /// <summary>
    /// Converts Western digits to Bengali ones. Separators and the minus sign are left alone —
    /// Bangladeshi printed accounts keep "," and "." even when the digits are Bengali.
    /// </summary>
    public static string ToBengaliNumerals(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        Span<char> buffer = text.Length <= 128 ? stackalloc char[text.Length] : new char[text.Length];
        var changed = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is >= '0' and <= '9')
            {
                buffer[i] = BengaliDigits[c - '0'][0];
                changed = true;
            }
            else
            {
                buffer[i] = c;
            }
        }

        return changed ? new string(buffer) : text;
    }

    /// <summary>Number-format string handed to Excel so the cell stays a real, pivotable number.</summary>
    public static string ExcelNumberFormat(ReportColumnType type, int decimals) => type switch
    {
        ReportColumnType.WholeNumber => "#,##0",
        ReportColumnType.Number or ReportColumnType.Money => decimals <= 0 ? "#,##0" : "#,##0." + new string('0', decimals),
        ReportColumnType.Percent => decimals <= 0 ? "0%" : "0." + new string('0', decimals) + "%",
        ReportColumnType.Date => "dd-mmm-yyyy",
        ReportColumnType.DateTime => "dd-mmm-yyyy hh:mm",
        _ => "@",
    };
}
