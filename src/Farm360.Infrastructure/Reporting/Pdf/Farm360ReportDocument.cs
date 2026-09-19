using System;
using System.Collections.Generic;
using System.Globalization;
using Farm360.Application.Reporting.Model;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Reporting.Pdf;

/// <summary>
/// The Farm360 house report, rendered as a banded document: letterhead, parameter echo,
/// repeating column header, detail rows, group sub-totals, grand total, page footer.
/// </summary>
/// <remarks>
/// Every report in the pack is this one document filled from a different <see cref="ReportDataSet"/>.
/// Keeping the skeleton in a single class is what makes thirty reports look like one product
/// instead of thirty screens, and it means a change to the house style is one edit, not thirty.
/// Layout constants follow docs/32 §5.
/// </remarks>
internal sealed class Farm360ReportDocument : IDocument
{
    private const float BodyFontSize = 8f;
    private const float HeaderFontSize = 8f;
    private const float TitleFontSize = 12f;
    private const float OrgFontSize = 13f;
    private const float FooterFontSize = 7f;
    private const float CellPadding = 2.5f;
    private const float RuleWidth = 0.5f;

    private static readonly Color HeaderFill = Colors.Grey.Lighten2;
    private static readonly Color GroupFill = Colors.Grey.Lighten3;
    private static readonly Color TotalFill = Colors.Grey.Lighten3;
    private static readonly Color Muted = Colors.Grey.Darken1;

    private static readonly string[] SignatureRoles = ["Prepared by", "Checked by", "Approved by"];

    private readonly ReportDataSet _data;

    public Farm360ReportDocument(ReportDataSet data)
    {
        _data = data;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = _data.Title,
        Subject = _data.Meta.Subject ?? _data.Title,
        Author = _data.Meta.OrganizationName,
        Creator = "Farm360 AI",
    };

    public void Compose(IDocumentContainer container) =>
        container.Page(page =>
        {
            page.Size(_data.Page.Orientation == ReportOrientation.Landscape
                ? PageSizes.A4.Landscape()
                : PageSizes.A4);

            page.Margin(_data.Page.MarginMm, Unit.Millimetre);

            // Noto Sans Bengali covers Bengali and Latin. Anything it lacks is picked up by
            // QuestPDF's automatic font fallback, so no explicit fallback chain is needed.
            page.DefaultTextStyle(x => x
                .FontFamily(ReportFonts.Primary)
                .FontSize(BodyFontSize)
                .FontColor(Colors.Black));

            page.Header().Element(ComposeLetterhead);
            page.Content().PaddingVertical(4).Element(ComposeBody);
            page.Footer().Element(ComposeFooter);
        });

    // ── Letterhead ──────────────────────────────────────────────────────────
    // Repeats on every page: a page that escapes the stapler must still identify itself.

    private void ComposeLetterhead(IContainer container) =>
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(_data.Meta.OrganizationName)
                .FontSize(OrgFontSize).Bold();

            if (!string.IsNullOrWhiteSpace(_data.Meta.FarmName))
            {
                col.Item().AlignCenter().Text(_data.Meta.FarmName!).FontSize(HeaderFontSize);
            }

            col.Item().PaddingTop(3).AlignCenter().Text(_data.Title)
                .FontSize(TitleFontSize).Bold();

            if (!string.IsNullOrWhiteSpace(_data.Meta.Subject))
            {
                col.Item().AlignCenter().Text(_data.Meta.Subject!).FontSize(HeaderFontSize).FontColor(Muted);
            }

            col.Item().PaddingTop(3).BorderBottom(1).BorderColor(Colors.Black);
        });

    private void ComposeBody(IContainer container) =>
        container.Column(col =>
        {
            // First page only — the reader has the parameters by the time page 2 arrives.
            if (_data.Meta.Parameters.Count > 0)
            {
                col.Item().ShowOnce().PaddingBottom(4).Element(ComposeParameterBox);
            }

            col.Item().Element(ComposeTable);

            if (_data.Page.ShowSignatureBlock)
            {
                col.Item().PaddingTop(14).Element(ComposeSignatures);
            }
        });

    // ── Parameter echo box ──────────────────────────────────────────────────
    // A printed page without its filters is unreadable evidence; this is what makes it evidence.

    private void ComposeParameterBox(IContainer container) =>
        container.Border(1).BorderColor(Colors.Black).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30, Unit.Millimetre);
                c.RelativeColumn();
                c.ConstantColumn(30, Unit.Millimetre);
                c.RelativeColumn();
            });

            var fields = new List<ReportMetaField>(_data.Meta.Parameters)
            {
                new("Generated by", _data.Meta.GeneratedByName),
                new("Generated at", _data.Meta.GeneratedAtUtc.ToString("dd-MMM-yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture)),
            };

            foreach (var field in fields)
            {
                table.Cell().Background(HeaderFill).Border(RuleWidth).Padding(CellPadding)
                    .Text(field.Label).FontSize(HeaderFontSize).Bold();

                table.Cell().Border(RuleWidth).Padding(CellPadding)
                    .Text(field.Value).FontSize(HeaderFontSize);
            }

            // Keep the grid rectangular when the field count is odd.
            if (fields.Count % 2 != 0)
            {
                table.Cell().Border(RuleWidth).Padding(CellPadding).Text(string.Empty);
                table.Cell().Border(RuleWidth).Padding(CellPadding).Text(string.Empty);
            }
        });

    // ── Detail band ─────────────────────────────────────────────────────────

    private void ComposeTable(IContainer container)
    {
        if (_data.Rows.Count == 0)
        {
            container.PaddingVertical(20).AlignCenter()
                .Text("No data available for the selected criteria.")
                .FontSize(10).FontColor(Muted);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                // Serial column — readers of a printed register count lines.
                columns.ConstantColumn(10, Unit.Millimetre);

                foreach (var column in _data.Columns)
                {
                    if (column.IsRelativeWidth)
                    {
                        columns.RelativeColumn(column.Width);
                    }
                    else
                    {
                        columns.ConstantColumn(column.Width, Unit.Millimetre);
                    }
                }
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "#", ReportAlign.Center);

                foreach (var column in _data.Columns)
                {
                    HeaderCell(header.Cell(), column.Header.En, column.Align);
                }
            });

            ComposeRows(table);
        });
    }

    private void ComposeRows(TableDescriptor table)
    {
        var printedGroupTotals = new HashSet<string>(StringComparer.Ordinal);
        string? currentGroup = null;
        var serial = 0;

        for (var i = 0; i < _data.Rows.Count; i++)
        {
            var row = _data.Rows[i];

            if (row.GroupKey is not null && !string.Equals(row.GroupKey, currentGroup, StringComparison.Ordinal))
            {
                if (currentGroup is not null)
                {
                    EmitGroupTotals(table, currentGroup, printedGroupTotals);
                }

                currentGroup = row.GroupKey;
                EmitGroupHeader(table, row.GroupKey);
            }

            serial++;
            BodyCell(table.Cell(), serial.ToString(CultureInfo.InvariantCulture), ReportAlign.Center);

            for (var c = 0; c < row.Cells.Count && c < _data.Columns.Count; c++)
            {
                BodyCell(table.Cell(), row.Cells[c].F, _data.Columns[c].Align);
            }
        }

        if (currentGroup is not null)
        {
            EmitGroupTotals(table, currentGroup, printedGroupTotals);
        }

        if (_data.HasGrandTotals)
        {
            EmitGrandTotals(table);
        }
    }

    private void EmitGroupHeader(TableDescriptor table, string groupKey)
    {
        var label = FindGroupLabel(groupKey);

        table.Cell().ColumnSpan((uint)(_data.Columns.Count + 1))
            .Background(GroupFill).Border(RuleWidth).Padding(CellPadding)
            .Text(label).FontSize(HeaderFontSize).Bold();
    }

    private void EmitGroupTotals(TableDescriptor table, string groupKey, HashSet<string> printed)
    {
        if (!printed.Add(groupKey))
        {
            // Defensive: a definition that does not order by its group key would otherwise
            // print the same sub-total several times, which reads as a data error.
            return;
        }

        foreach (var group in _data.Groups)
        {
            if (!string.Equals(group.GroupKey, groupKey, StringComparison.Ordinal))
            {
                continue;
            }

            TotalCell(table.Cell(), string.Empty, ReportAlign.Center, GroupFill);
            var label = $"Sub-total — {group.GroupLabel} ({group.RowCount})";
            var labelPlaced = false;

            for (var c = 0; c < _data.Columns.Count; c++)
            {
                var value = c < group.Totals.Count ? group.Totals[c].F : string.Empty;

                if (!labelPlaced && string.IsNullOrEmpty(value))
                {
                    TotalCell(table.Cell(), label, ReportAlign.Left, GroupFill);
                    labelPlaced = true;
                    continue;
                }

                TotalCell(table.Cell(), value, _data.Columns[c].Align, GroupFill);
            }

            return;
        }
    }

    private void EmitGrandTotals(TableDescriptor table)
    {
        TotalCell(table.Cell(), string.Empty, ReportAlign.Center, TotalFill, topRule: true);
        var labelPlaced = false;

        for (var c = 0; c < _data.Columns.Count; c++)
        {
            var value = c < _data.GrandTotals.Count ? _data.GrandTotals[c].F : string.Empty;

            if (!labelPlaced && string.IsNullOrEmpty(value))
            {
                TotalCell(table.Cell(), $"GRAND TOTAL ({_data.TotalRowCount} rows)", ReportAlign.Left, TotalFill, topRule: true);
                labelPlaced = true;
                continue;
            }

            TotalCell(table.Cell(), value, _data.Columns[c].Align, TotalFill, topRule: true);
        }
    }

    private string FindGroupLabel(string groupKey)
    {
        foreach (var group in _data.Groups)
        {
            if (string.Equals(group.GroupKey, groupKey, StringComparison.Ordinal))
            {
                return group.GroupLabel;
            }
        }

        return groupKey;
    }

    // ── Cell primitives ─────────────────────────────────────────────────────

    private static void HeaderCell(IContainer cell, string text, ReportAlign align) =>
        Align(cell.Background(HeaderFill).Border(RuleWidth).Padding(CellPadding), align)
            .Text(text).FontSize(HeaderFontSize).Bold();

    // ShowEntire keeps a wrapping cell from being sliced by a page break, which would otherwise
    // strand the tail of one detail row at the top of the next page with every other column blank.
    private static void BodyCell(IContainer cell, string text, ReportAlign align) =>
        Align(cell.ShowEntire().Border(RuleWidth).Padding(CellPadding), align)
            .Text(text).FontSize(BodyFontSize);

    private static void TotalCell(IContainer cell, string text, ReportAlign align, Color fill, bool topRule = false)
    {
        var container = cell.Background(fill).Border(RuleWidth);

        if (topRule)
        {
            container = container.BorderTop(1.5f).BorderColor(Colors.Black);
        }

        Align(container.Padding(CellPadding), align).Text(text).FontSize(HeaderFontSize).Bold();
    }

    private static IContainer Align(IContainer container, ReportAlign align) => align switch
    {
        ReportAlign.Right => container.AlignRight(),
        ReportAlign.Center => container.AlignCenter(),
        _ => container.AlignLeft(),
    };

    // ── Signatures ──────────────────────────────────────────────────────────
    // Financial and valuation documents only — a management report nobody signs
    // should not pretend to carry authority.

    private static void ComposeSignatures(IContainer container) =>
        container.Row(row =>
        {
            foreach (var role in SignatureRoles)
            {
                row.RelativeItem().PaddingHorizontal(6).Column(col =>
                {
                    col.Item().PaddingTop(16).BorderTop(RuleWidth).BorderColor(Colors.Black);
                    col.Item().AlignCenter().Text(role).FontSize(FooterFontSize);
                });
            }
        });

    // ── Page footer ─────────────────────────────────────────────────────────

    private void ComposeFooter(IContainer container) =>
        container.BorderTop(RuleWidth).BorderColor(Colors.Black).PaddingTop(2).Row(row =>
        {
            // The run id makes any loose printed page traceable back to its ReportRun audit row.
            row.RelativeItem().Text($"{_data.Meta.ReportKey} · run {ShortRunId(_data.Meta.RunId)}")
                .FontSize(FooterFontSize).FontColor(Muted);

            if (!_data.Page.ShowPageNumbers)
            {
                return;
            }

            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(FooterFontSize).FontColor(Muted));
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        });

    private static string ShortRunId(Guid runId) =>
        runId == Guid.Empty ? "preview" : runId.ToString("N", CultureInfo.InvariantCulture)[..8];
}
