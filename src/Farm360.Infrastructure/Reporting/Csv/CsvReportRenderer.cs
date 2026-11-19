using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
/// <para>
/// Raw values, not display strings: CSV is a machine hand-off, so a consumer parsing "1,234.50"
/// as money would have to undo our thousands separators first. The PDF and the spreadsheet carry
/// the presentation; this carries the data.
/// </para>
/// <para>
/// Grouped reports keep their grouping as data: a leading Group column names each row's group and
/// a Row type column marks detail, sub-total and grand-total rows, so a consumer can filter totals
/// out (or keep only them) instead of losing the structure.
/// </para>
/// <para>
/// Text cells beginning with <c>= + - @</c> (or a tab / carriage return) are prefixed with a single
/// quote. Spreadsheet programs execute such cells as formulas when the CSV is opened, and an animal
/// note or supplier name is user input — "CSV injection" is an export turning into an attack.
/// </para>
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

        var grouped = dataSet.Groups.Count > 0;
        var builder = new StringBuilder(dataSet.Rows.Count * 96 + 256);

        var header = new List<string>(dataSet.Columns.Count + 2);
        if (grouped)
        {
            header.Add(dataSet.Text(ReportStrings.Group));
        }

        header.Add(dataSet.Text(ReportStrings.RowType));
        header.AddRange(dataSet.Columns.Select(c => c.Header.For(dataSet.Language)));
        AppendLine(builder, header);

        var groups = dataSet.Groups.ToDictionary(g => g.GroupKey, StringComparer.Ordinal);
        var detail = dataSet.Text(ReportStrings.Detail);
        string? currentGroup = null;

        foreach (var row in dataSet.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (grouped && !string.Equals(row.GroupKey, currentGroup, StringComparison.Ordinal))
            {
                if (currentGroup is not null)
                {
                    AppendSubtotal(builder, dataSet, groups, currentGroup);
                }

                currentGroup = row.GroupKey;
            }

            var fields = new List<string>(dataSet.Columns.Count + 2);
            if (grouped)
            {
                fields.Add(GuardText(GroupLabel(groups, row.GroupKey)));
            }

            fields.Add(detail);
            for (var c = 0; c < dataSet.Columns.Count; c++)
            {
                fields.Add(RawText(c < row.Cells.Count ? row.Cells[c] : null, dataSet.Columns[c]));
            }

            AppendLine(builder, fields);
        }

        if (grouped && currentGroup is not null)
        {
            AppendSubtotal(builder, dataSet, groups, currentGroup);
        }

        if (dataSet.HasGrandTotals)
        {
            var fields = new List<string>(dataSet.Columns.Count + 2);
            if (grouped)
            {
                fields.Add(string.Empty);
            }

            fields.Add(GuardText(dataSet.GrandTotalCaption));
            for (var c = 0; c < dataSet.Columns.Count; c++)
            {
                fields.Add(RawText(c < dataSet.GrandTotals.Count ? dataSet.GrandTotals[c] : null, dataSet.Columns[c]));
            }

            AppendLine(builder, fields);
        }

        // UTF-8 with BOM so Excel opens Bengali column headers correctly instead of mojibake.
        return Task.FromResult(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString()));
    }

    private static void AppendSubtotal(
        StringBuilder builder,
        ReportDataSet dataSet,
        Dictionary<string, ReportGroupTotals> groups,
        string groupKey)
    {
        if (!groups.TryGetValue(groupKey, out var group) || group.Totals.All(t => t.V is null))
        {
            return;
        }

        var fields = new List<string>(dataSet.Columns.Count + 2)
        {
            GuardText(group.GroupLabel),
            dataSet.Text(ReportStrings.SubTotal),
        };

        for (var c = 0; c < dataSet.Columns.Count; c++)
        {
            fields.Add(RawText(c < group.Totals.Count ? group.Totals[c] : null, dataSet.Columns[c]));
        }

        AppendLine(builder, fields);
    }

    private static string GroupLabel(Dictionary<string, ReportGroupTotals> groups, string? key) =>
        key is not null && groups.TryGetValue(key, out var g) ? g.GroupLabel : key ?? string.Empty;

    private static string RawText(ReportCell? cell, ReportColumn column)
    {
        if (cell?.V is null)
        {
            return string.Empty;
        }

        return column.Type switch
        {
            // Numbers stay numbers — a negative amount legitimately starts with "-".
            ReportColumnType.WholeNumber or ReportColumnType.Number or ReportColumnType.Money or ReportColumnType.Percent =>
                ReportValueFormatter.ToDecimal(cell.V).ToString(CultureInfo.InvariantCulture),

            ReportColumnType.Date => cell.V switch
            {
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => GuardText(cell.F),
            },

            ReportColumnType.DateTime => cell.V switch
            {
                DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                _ => GuardText(cell.F),
            },

            ReportColumnType.Boolean => cell.V is bool b && b ? "true" : "false",

            _ => GuardText(cell.V.ToString() ?? string.Empty),
        };
    }

    /// <summary>
    /// Neutralises spreadsheet formula injection: a text value that a spreadsheet would execute
    /// is prefixed with a single quote, which displays the value literally.
    /// </summary>
    public static string GuardText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1859", Justification = "Called with header arrays and row lists alike.")]
    private static void AppendLine(StringBuilder builder, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendEscaped(builder, fields[i]);
        }

        builder.Append('\n');
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
