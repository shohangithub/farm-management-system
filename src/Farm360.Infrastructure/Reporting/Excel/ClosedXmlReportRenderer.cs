using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Infrastructure.Reporting.Excel;

/// <summary>
/// Renders a report to a real .xlsx workbook.
/// </summary>
/// <remarks>
/// <para>
/// Numbers are written as numbers with a display format, never as pre-formatted strings.
/// The Accounts Head's first move with any export is to sum a column or drop it into a pivot
/// table; a spreadsheet full of text that merely looks like money makes that impossible, which
/// is the difference between an export and a screenshot.
/// </para>
/// <para>
/// Grouped reports keep their structure: a bold band names each group, a sub-total row follows
/// it, and every total is a live <c>SUBTOTAL</c> formula over the rows above it — so a user who
/// corrects a figure sees the totals follow, and the grand total (also <c>SUBTOTAL</c>) never
/// double-counts the sub-totals.
/// </para>
/// </remarks>
public sealed class ClosedXmlReportRenderer : IReportRenderer
{
    private const int MetaStartRow = 1;

    /// <summary>Above this many columns a portrait page cannot hold the table legibly.</summary>
    private const int WideReportColumns = 7;

    private static readonly XLColor GroupFill = XLColor.FromHtml("#E7E6E6");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#F2F2F2");

    public ReportExportFormat Format => ReportExportFormat.Xlsx;

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string FileExtension => "xlsx";

    public Task<byte[]> RenderAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(dataSet.Title));

        var row = WriteMetaBlock(sheet, dataSet);
        var headerRow = row + 1;

        WriteHeader(sheet, dataSet, headerRow);
        var (lastRow, detailRows) = WriteBody(sheet, dataSet, headerRow + 1, cancellationToken);

        var notesRow = WriteSummary(sheet, dataSet, lastRow + 2);
        WriteNotes(sheet, dataSet, notesRow);
        Finish(sheet, dataSet, headerRow, lastRow, detailRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return Task.FromResult(stream.ToArray());
    }

    private static int WriteMetaBlock(IXLWorksheet sheet, ReportDataSet data)
    {
        var row = MetaStartRow;

        sheet.Cell(row, 1).Value = data.Meta.OrganizationName;
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(13);
        row++;

        // The organisation's address · phone · e-mail, so a forwarded spreadsheet still says whose it is.
        if (!string.IsNullOrWhiteSpace(data.Meta.ContactLine))
        {
            sheet.Cell(row, 1).Value = data.Meta.ContactLine;
            sheet.Cell(row, 1).Style.Font.SetFontColor(XLColor.FromHtml("#555555"));
            row++;
        }

        if (!string.IsNullOrWhiteSpace(data.Meta.FarmName))
        {
            sheet.Cell(row, 1).Value = data.Meta.FarmName;
            sheet.Cell(row, 1).Style.Font.SetBold();
            row++;
        }

        sheet.Cell(row, 1).Value = data.Title;
        sheet.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(11);
        row++;

        if (!string.IsNullOrWhiteSpace(data.Meta.Subject))
        {
            sheet.Cell(row, 1).Value = data.Meta.Subject;
            row++;
        }

        foreach (var field in data.Meta.Parameters)
        {
            sheet.Cell(row, 1).Value = field.Label;
            sheet.Cell(row, 1).Style.Font.SetBold();
            sheet.Cell(row, 2).Value = field.Value;
            row++;
        }

        sheet.Cell(row, 1).Value = data.Text(ReportStrings.Generated);
        sheet.Cell(row, 1).Style.Font.SetBold();
        sheet.Cell(row, 2).Value =
            $"{data.Meta.GeneratedByName} · {data.Meta.GeneratedAtDisplay} · {data.Text(ReportStrings.Run)} {data.Meta.RunId:N}";

        return row + 1;
    }

    private static void WriteHeader(IXLWorksheet sheet, ReportDataSet data, int headerRow)
    {
        for (var c = 0; c < data.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = data.Columns[c].Header.For(data.Language);
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
            cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
            cell.Style.Alignment.SetWrapText(true);
        }
    }

    /// <summary>Detail rows, group bands, sub-totals and the grand total. Returns the last row used.</summary>
    private static (int LastRow, int DetailRows) WriteBody(IXLWorksheet sheet, ReportDataSet data, int firstRow, CancellationToken cancellationToken)
    {
        var colCount = data.Columns.Count;
        var sheetRow = firstRow;

        if (data.Rows.Count == 0)
        {
            sheet.Cell(sheetRow, 1).Value = data.Text(ReportStrings.NoData);
            sheet.Cell(sheetRow, 1).Style.Font.SetItalic();
            return (sheetRow, 0);
        }

        var groups = data.Groups.ToDictionary(g => g.GroupKey, StringComparer.Ordinal);
        string? currentGroup = null;
        var groupStart = sheetRow;
        var groupRows = 0;

        foreach (var reportRow in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reportRow.GroupKey is { } key && !string.Equals(key, currentGroup, StringComparison.Ordinal))
            {
                if (currentGroup is not null)
                {
                    sheetRow = WriteSubtotal(sheet, data, groups, currentGroup, groupStart, sheetRow - 1, groupRows, sheetRow);
                }

                currentGroup = key;
                sheetRow = WriteGroupHeader(sheet, data, groups.TryGetValue(key, out var g) ? g.GroupLabel : key, sheetRow);
                groupStart = sheetRow;
                groupRows = 0;
            }

            for (var c = 0; c < reportRow.Cells.Count && c < colCount; c++)
            {
                WriteValue(sheet.Cell(sheetRow, c + 1), reportRow.Cells[c], data.Columns[c]);
            }

            groupRows++;
            sheetRow++;
        }

        var lastDetailRow = sheetRow - 1;

        if (currentGroup is not null)
        {
            sheetRow = WriteSubtotal(sheet, data, groups, currentGroup, groupStart, lastDetailRow, groupRows, sheetRow);
        }

        if (data.HasGrandTotals)
        {
            WriteTotalRow(
                sheet, data, data.GrandTotals, firstRow, lastDetailRow, sheetRow,
                $"{data.GrandTotalCaption} ({data.Count(data.TotalRowCount)} {data.Text(ReportStrings.Rows)})",
                TotalFill, XLBorderStyleValues.Double);
            sheetRow++;
        }

        return (sheetRow - 1, data.Rows.Count);
    }

    /// <summary>
    /// The summary box (headline figures, then findings) under the table — kept below it so the
    /// header row, filter and frozen panes stay where every other report has them. Returns the
    /// next free row.
    /// </summary>
    private static int WriteSummary(IXLWorksheet sheet, ReportDataSet data, int row)
    {
        if (data.Summary is not { } summary)
            return row;

        var width = Math.Max(2, data.Columns.Count);
        var title = sheet.Range(row, 1, row, width);
        title.Merge();
        title.Value = summary.Title;
        title.Style.Font.SetBold();
        title.Style.Fill.SetBackgroundColor(GroupFill);

        foreach (var figure in summary.Figures)
        {
            row++;
            sheet.Cell(row, 1).Value = figure.Label;
            var value = sheet.Range(row, 2, row, width);
            value.Merge();
            value.Value = figure.Value;
            value.Style.Font.SetBold();
            if (figure.Tone == ReportTone.Good) value.Style.Font.SetFontColor(XLColor.FromHtml("#1B5E20"));
            if (figure.Tone == ReportTone.Bad) value.Style.Font.SetFontColor(XLColor.FromHtml("#B71C1C"));
        }

        if (summary.Insights.Count > 0)
        {
            row += 2;
            if (!string.IsNullOrWhiteSpace(summary.InsightsTitle))
            {
                var head = sheet.Range(row, 1, row, width);
                head.Merge();
                head.Value = summary.InsightsTitle;
                head.Style.Font.SetBold();
                row++;
            }

            foreach (var insight in summary.Insights)
            {
                var line = sheet.Range(row, 1, row, width);
                line.Merge();
                line.Value = "• " + insight;
                line.Style.Alignment.SetWrapText(true);
                row++;
            }
        }

        return row + 1;
    }

    /// <summary>"How to read this report" lines under the table, one per row, merged across the columns.</summary>
    private static void WriteNotes(IXLWorksheet sheet, ReportDataSet data, int row)
    {
        if (data.NotesOrEmpty.Count == 0)
            return;

        var width = Math.Max(1, data.Columns.Count);
        var title = sheet.Range(row, 1, row, width);
        title.Merge();
        title.Value = data.Text(ReportStrings.HowToRead);
        title.Style.Font.SetBold();

        foreach (var note in data.NotesOrEmpty)
        {
            row++;
            var line = sheet.Range(row, 1, row, width);
            line.Merge();
            line.Value = "• " + note;
            line.Style.Alignment.SetWrapText(true);
            line.Style.Font.SetFontColor(XLColor.FromHtml("#444444"));
        }
    }

    private static int WriteGroupHeader(IXLWorksheet sheet, ReportDataSet data, string label, int row)
    {
        var range = sheet.Range(row, 1, row, Math.Max(1, data.Columns.Count));
        range.Merge();
        range.Value = label;
        range.Style.Font.SetBold();
        range.Style.Fill.SetBackgroundColor(GroupFill);
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
        return row + 1;
    }

    private static int WriteSubtotal(
        IXLWorksheet sheet,
        ReportDataSet data,
        Dictionary<string, ReportGroupTotals> groups,
        string groupKey,
        int firstDetailRow,
        int lastDetailRow,
        int groupRows,
        int row)
    {
        if (!groups.TryGetValue(groupKey, out var group) || group.Totals.All(t => t.V is null))
        {
            // Nothing to total in this group (a statement section, a status list): no empty band.
            return row;
        }

        WriteTotalRow(
            sheet, data, group.Totals, firstDetailRow, lastDetailRow, row,
            $"{data.Text(ReportStrings.SubTotal)} — {group.GroupLabel} ({data.Count(groupRows)})",
            GroupFill, XLBorderStyleValues.Thin);

        return row + 1;
    }

    /// <summary>
    /// One totals row. Summable columns get a SUBTOTAL formula over the detail range; anything the
    /// platform declined to total (mixed units, percentages) stays blank rather than invented here.
    /// </summary>
    private static void WriteTotalRow(
        IXLWorksheet sheet,
        ReportDataSet data,
        IReadOnlyList<ReportCell> totals,
        int firstDetailRow,
        int lastDetailRow,
        int row,
        string label,
        XLColor fill,
        XLBorderStyleValues topBorder)
    {
        var labelPlaced = false;

        for (var c = 0; c < data.Columns.Count; c++)
        {
            var cell = sheet.Cell(row, c + 1);
            var column = data.Columns[c];
            var total = c < totals.Count ? totals[c] : null;

            if (total?.V is not null)
            {
                var function = column.Aggregate switch
                {
                    ReportAggregate.Sum => 9,
                    ReportAggregate.Average => 1,
                    ReportAggregate.Min => 5,
                    ReportAggregate.Max => 4,
                    _ => 0,
                };

                if (function > 0 && lastDetailRow >= firstDetailRow && IsNumeric(column.Type))
                {
                    var letter = sheet.Column(c + 1).ColumnLetter();
                    cell.FormulaA1 = $"SUBTOTAL({function},{letter}{firstDetailRow}:{letter}{lastDetailRow})";
                    cell.Style.NumberFormat.Format = ReportValueFormatter.ExcelNumberFormat(column.Type, column.Decimals);
                    cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                }
                else
                {
                    WriteValue(cell, total, column);
                }
            }
            else if (!labelPlaced)
            {
                cell.Value = label;
                labelPlaced = true;
            }

            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(fill);
            cell.Style.Border.SetTopBorder(topBorder);
        }
    }

    private static bool IsNumeric(ReportColumnType type) =>
        type is ReportColumnType.WholeNumber or ReportColumnType.Number or ReportColumnType.Money or ReportColumnType.Percent;

    private static void WriteValue(IXLCell cell, ReportCell source, ReportColumn column)
    {
        if (source.V is null)
        {
            return;
        }

        switch (column.Type)
        {
            case ReportColumnType.WholeNumber:
            case ReportColumnType.Number:
            case ReportColumnType.Money:
            // Excel's percent format multiplies by 100 itself, so the ratio goes in raw.
            case ReportColumnType.Percent:
                cell.Value = ReportValueFormatter.ToDecimal(source.V);
                cell.Style.NumberFormat.Format = ReportValueFormatter.ExcelNumberFormat(column.Type, column.Decimals);
                break;

            case ReportColumnType.Date:
            case ReportColumnType.DateTime:
                if (TryGetDateTime(source.V, out var dateValue))
                {
                    cell.Value = dateValue;
                    cell.Style.NumberFormat.Format = ReportValueFormatter.ExcelNumberFormat(column.Type, 0);
                }
                else
                {
                    cell.Value = source.F;
                }

                break;

            case ReportColumnType.Boolean:
                cell.Value = source.V is bool b && b;
                break;

            default:
                // Text is written as text: a tag like "0042" or a voucher "=X" must not be
                // reinterpreted as a number or a formula.
                cell.SetValue(source.F);
                break;
        }

        if (column.Align == ReportAlign.Right)
        {
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }
        else if (column.Align == ReportAlign.Center)
        {
            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }
    }

    private static bool TryGetDateTime(object value, out DateTime result)
    {
        switch (value)
        {
            case DateTime dt:
                result = dt;
                return true;
            case DateOnly d:
                result = d.ToDateTime(TimeOnly.MinValue);
                return true;
            case DateTimeOffset dto:
                result = dto.UtcDateTime;
                return true;
            default:
                result = default;
                return false;
        }
    }

    private static void Finish(IXLWorksheet sheet, ReportDataSet data, int headerRow, int lastRow, int detailRows)
    {
        if (data.Columns.Count > 0 && detailRows > 0)
        {
            sheet.SheetView.FreezeRows(headerRow);

            // Autofilter only on flat registers: inside a grouped sheet, filtering would mix the
            // band and sub-total rows in with the detail and the SUBTOTALs would no longer match.
            if (data.Groups.Count == 0)
            {
                sheet.Range(headerRow, 1, lastRow - (data.HasGrandTotals ? 1 : 0), data.Columns.Count).SetAutoFilter();
            }
        }

        sheet.Columns().AdjustToContents(5d, 45d);

        var landscape = data.Page.Orientation == ReportOrientation.Landscape || data.Columns.Count > WideReportColumns;
        sheet.PageSetup.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;

        // One page wide, as many tall as needed: a printed register must never split its columns.
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.PrintAreas.Add(1, 1, Math.Max(lastRow, headerRow), Math.Max(data.Columns.Count, 1));
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }

    /// <summary>Excel rejects several characters in sheet names and caps them at 31 chars.</summary>
    private static string SafeSheetName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Report";
        }

        Span<char> buffer = stackalloc char[Math.Min(title.Length, 31)];
        for (var i = 0; i < buffer.Length; i++)
        {
            var c = title[i];
            buffer[i] = c is '\\' or '/' or '*' or '?' or ':' or '[' or ']' ? '-' : c;
        }

        return new string(buffer);
    }
}
