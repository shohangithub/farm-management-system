using System;
using System.Globalization;
using System.IO;
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
/// Numbers are written as numbers with a display format, never as pre-formatted strings.
/// The Accounts Head's first move with any export is to sum a column or drop it into a pivot
/// table; a spreadsheet full of text that merely looks like money makes that impossible, which
/// is the difference between an export and a screenshot.
/// </remarks>
public sealed class ClosedXmlReportRenderer : IReportRenderer
{
    private const int MetaStartRow = 1;

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
        var lastDataRow = WriteRows(sheet, dataSet, headerRow + 1, cancellationToken);
        WriteTotals(sheet, dataSet, lastDataRow + 1);

        Finish(sheet, dataSet, headerRow, lastDataRow);

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

        sheet.Cell(row, 1).Value = "Generated";
        sheet.Cell(row, 1).Style.Font.SetBold();
        sheet.Cell(row, 2).Value =
            $"{data.Meta.GeneratedByName} · {data.Meta.GeneratedAtUtc.ToString("dd-MMM-yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)} · run {data.Meta.RunId:N}";

        return row + 1;
    }

    private static void WriteHeader(IXLWorksheet sheet, ReportDataSet data, int headerRow)
    {
        for (var c = 0; c < data.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = data.Columns[c].Header.En;
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
            cell.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
        }
    }

    private static int WriteRows(IXLWorksheet sheet, ReportDataSet data, int firstRow, CancellationToken cancellationToken)
    {
        var sheetRow = firstRow;

        foreach (var reportRow in data.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var c = 0; c < reportRow.Cells.Count && c < data.Columns.Count; c++)
            {
                WriteValue(sheet.Cell(sheetRow, c + 1), reportRow.Cells[c], data.Columns[c]);
            }

            sheetRow++;
        }

        return sheetRow - 1;
    }

    private static void WriteTotals(IXLWorksheet sheet, ReportDataSet data, int totalsRow)
    {
        if (!data.HasGrandTotals)
        {
            return;
        }

        for (var c = 0; c < data.Columns.Count; c++)
        {
            var cell = sheet.Cell(totalsRow, c + 1);

            if (c < data.GrandTotals.Count && data.GrandTotals[c].V is not null)
            {
                WriteValue(cell, data.GrandTotals[c], data.Columns[c]);
            }
            else if (c == 0)
            {
                cell.Value = "GRAND TOTAL";
            }

            cell.Style.Font.SetBold();
            cell.Style.Border.SetTopBorder(XLBorderStyleValues.Double);
        }
    }

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
                cell.Value = ReportValueFormatter.ToDecimal(source.V);
                cell.Style.NumberFormat.Format = ReportValueFormatter.ExcelNumberFormat(column.Type, column.Decimals);
                break;

            case ReportColumnType.Percent:
                // Excel's percent format multiplies by 100 itself, so the ratio goes in raw.
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
                cell.Value = source.F;
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

    private static void Finish(IXLWorksheet sheet, ReportDataSet data, int headerRow, int lastDataRow)
    {
        if (data.Columns.Count > 0 && lastDataRow >= headerRow)
        {
            // Freeze under the header and turn on autofilter: the first thing anyone does with a
            // long register is scroll and filter it.
            sheet.SheetView.FreezeRows(headerRow);
            sheet.Range(headerRow, 1, lastDataRow, data.Columns.Count).SetAutoFilter();
        }

        sheet.Columns().AdjustToContents(5d, 45d);
        sheet.PageSetup.PrintAreas.Add(1, 1, Math.Max(lastDataRow + 2, headerRow), Math.Max(data.Columns.Count, 1));
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
