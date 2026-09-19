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
    int TotalRowCount);
