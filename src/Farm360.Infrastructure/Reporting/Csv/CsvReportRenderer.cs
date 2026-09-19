using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Infrastructure.Reporting.Csv;

/// <summary>
/// Renders a report to CSV for downstream tools and data transfer.
/// </summary>
/// <remarks>
/// Raw values, not display strings: CSV is a machine hand-off, so a consumer parsing "1,234.50"
/// as money would have to undo our thousands separators first. The PDF and the spreadsheet carry
/// the presentation; this carries the data.
/// </remarks>
public sealed class CsvReportRenderer : IReportRenderer
{
    public ReportExportFormat Format => ReportExportFormat.Csv;

    public string ContentType => "text/csv";

    public string FileExtension => "csv";

    public Task<byte[]> RenderAsync(ReportDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        cancellationToken.ThrowIfCancellationRequested();

        var builder = new StringBuilder(dataSet.Rows.Count * 96 + 256);

        for (var c = 0; c < dataSet.Columns.Count; c++)
        {
            if (c > 0)
            {
                builder.Append(',');
            }

            AppendEscaped(builder, dataSet.Columns[c].Header.En);
        }

        builder.Append('\n');

        foreach (var row in dataSet.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var c = 0; c < dataSet.Columns.Count; c++)
            {
                if (c > 0)
                {
                    builder.Append(',');
                }

                AppendEscaped(builder, RawText(c < row.Cells.Count ? row.Cells[c] : null, dataSet.Columns[c]));
            }

            builder.Append('\n');
        }

        // UTF-8 with BOM so Excel opens Bengali column headers correctly instead of mojibake.
        return Task.FromResult(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString()));
    }

    private static string RawText(ReportCell? cell, ReportColumn column)
    {
        if (cell?.V is null)
        {
            return string.Empty;
        }

        return column.Type switch
        {
            ReportColumnType.WholeNumber or ReportColumnType.Number or ReportColumnType.Money or ReportColumnType.Percent =>
                ReportValueFormatter.ToDecimal(cell.V).ToString(CultureInfo.InvariantCulture),

            ReportColumnType.Date => cell.V switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => cell.F,
            },

            ReportColumnType.DateTime => cell.V switch
            {
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => cell.F,
            },

            ReportColumnType.Boolean => cell.V is bool b && b ? "true" : "false",

            _ => cell.V.ToString() ?? string.Empty,
        };
    }

    private static void AppendEscaped(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var needsQuotes = value.AsSpan().IndexOfAny(',', '"', '\n') >= 0 || value.Contains('\r', StringComparison.Ordinal);

        if (!needsQuotes)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        foreach (var c in value)
        {
            if (c == '"')
            {
                builder.Append('"');
            }

            builder.Append(c);
        }

        builder.Append('"');
    }
}
