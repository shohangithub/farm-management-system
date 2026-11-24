using System.Collections.Generic;

namespace Farm360.Application.Reporting.Model;

/// <summary>
/// One rendered cell. <paramref name="V"/> is the raw value (Excel writes this as a real number
/// so an accountant can pivot it); <paramref name="F"/> is the server-formatted display string.
/// </summary>
/// <remarks>
/// Both are carried deliberately. The PDF, the spreadsheet and the on-screen viewer all print
/// <paramref name="F"/>, so they cannot disagree about rounding or thousands separators. If the
/// browser formatted its own numbers we would have two implementations of the same rules and,
/// eventually, a screen that contradicts the printed page.
/// </remarks>
public sealed record ReportCell(object? V, string F);

public sealed record ReportRow(IReadOnlyList<ReportCell> Cells, string? GroupKey = null);

/// <summary>Footer totals for one group, positioned after that group's last detail row.</summary>
public sealed record ReportGroupTotals(
    string GroupKey,
    string GroupLabel,
    int RowCount,
    IReadOnlyList<ReportCell> Totals);

/// <summary>
/// The complete, renderer-agnostic result of running a report. Every output format —
/// PDF, XLSX, CSV, and the JSON the Angular viewer consumes — is produced from this one object.
/// </summary>
public sealed record ReportDataSet(
    string Key,
    string Title,
    ReportCategory Category,
    PageSetup Page,
    ReportMetaHeader Meta,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<ReportRow> Rows,
    IReadOnlyList<ReportGroupTotals> Groups,
    IReadOnlyList<ReportCell> GrandTotals,
    bool HasGrandTotals,
    int TotalRowCount,
    ReportLanguage Language = ReportLanguage.En,
    bool BengaliNumerals = false,
    string? GrandTotalLabel = null,
    IReadOnlyList<string>? Notes = null,
    ReportSummary? Summary = null)
{
    /// <summary>"How to read this report" lines, in the report's language (empty when none).</summary>
    public IReadOnlyList<string> NotesOrEmpty => Notes ?? [];

    /// <summary>
    /// The label printed on the grand-total band. Statements override it ("Net profit / (loss)")
    /// so the bottom line says what it is instead of a generic "Grand total".
    /// </summary>
    public string GrandTotalCaption => GrandTotalLabel ?? ReportStrings.GrandTotal.For(Language);

    /// <summary>A platform string in this report's language.</summary>
    public string Text(LocalizedText text) => text.For(Language);

    /// <summary>A whole number in this report's numeral system.</summary>
    public string Count(int value) =>
        ReportValueFormatter.Format(value, ReportColumnType.WholeNumber, 0, BengaliNumerals, Language);
}

/// <summary>How a headline figure should be read: good news, bad news, or just a fact.</summary>
public static class ReportTone
{
    public const string Neutral = "neutral";
    public const string Good = "good";
    public const string Bad = "bad";
}

/// <summary>One headline figure in a report's summary box ("Net profit", "12,500.00 BDT").</summary>
public sealed record ReportSummaryFigure(string Label, string Value, string Tone = ReportTone.Neutral);

/// <summary>
/// The box printed above the table: a handful of headline figures, then plain-language findings
/// worked out from the rows ("B-04 lost 5,000 BDT — feed cost was 61% of its cost"). Built by
/// the report itself, in the report's language.
/// </summary>
public sealed record ReportSummary(
    string Title,
    IReadOnlyList<ReportSummaryFigure> Figures,
    string? InsightsTitle,
    IReadOnlyList<string> Insights);
