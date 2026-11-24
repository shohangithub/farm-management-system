using System;
using System.Collections.Generic;
using Farm360.Application.Documents;
using Farm360.Application.Reporting.Model;
using Farm360.Infrastructure.Reporting;
using Farm360.Infrastructure.Reporting.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Documents;

/// <summary>A label and its value in a document's details block ("Invoice no." → "SAL-…").</summary>
internal sealed record DocumentField(string Label, string Value);

/// <summary>One line of a totals box; <paramref name="Emphasis"/> rules it off and bolds it.</summary>
internal sealed record DocumentTotal(string Label, string Value, bool Emphasis = false);

/// <summary>
/// The house style shared by every customer document: A4 portrait, the report pack's letterhead,
/// a party box, a details block, ruled tables, a totals box, signature lines and a page footer.
/// Black on white with light grey rules, so it prints cleanly on any office printer.
/// </summary>
internal static class DocumentPdf
{
    public const float BodyFontSize = 9f;
    public const float SmallFontSize = 7.5f;
    public const float TitleFontSize = 15f;
    public const float CellPaddingVertical = 4f;
    public const float CellPaddingHorizontal = 4f;
    public const float RuleWidth = 0.5f;

    public static readonly Color HeaderFill = Colors.Grey.Lighten3;
    public static readonly Color RuleColor = Colors.Grey.Lighten1;
    public static readonly Color Muted = Colors.Grey.Darken1;

    /// <summary>Page geometry, fonts, letterhead and footer; <paramref name="content"/> fills the body.</summary>
    public static void Page(PageDescriptor page, DocumentHeader header, string title, Action<IContainer> content)
    {
        page.Size(PageSizes.A4);
        page.Margin(14, Unit.Millimetre);

        // Noto Sans Bengali covers Bengali and Latin, exactly as in the report pack.
        page.DefaultTextStyle(x => x
            .FontFamily(ReportFonts.Primary)
            .FontSize(BodyFontSize)
            .FontColor(Colors.Black));

        page.Header().Element(c => Letterhead(c, header, title));
        page.Content().PaddingTop(8).Element(content);
        page.Footer().Element(c => Footer(c, header));
    }

    public static DocumentMetadata Metadata(DocumentHeader header, string title) => new()
    {
        Title = title,
        Subject = title,
        Author = header.OrganizationName,
        Creator = "Farm360 AI",
    };

    private static void Letterhead(IContainer container, DocumentHeader header, string title) =>
        LetterheadComposer.Compose(
            container,
            new LetterheadContent(header.OrganizationName, header.FarmName, header.ContactLine, header.Logo),
            col => col.Item().PaddingTop(5).AlignCenter().Text(title).FontSize(TitleFontSize).Bold());

    private static void Footer(IContainer container, DocumentHeader header) =>
        container.BorderTop(RuleWidth).BorderColor(RuleColor).PaddingTop(3).Row(row =>
        {
            row.RelativeItem()
                .Text($"{header.Text(DocumentStrings.ComputerGenerated)} · {header.GeneratedByName} · {header.GeneratedAtDisplay}")
                .FontSize(SmallFontSize - 0.5f).FontColor(Muted);

            row.ConstantItem(32, Unit.Millimetre).AlignRight().Text(text =>
            {
                text.DefaultTextStyle(s => s.FontSize(SmallFontSize - 0.5f).FontColor(Muted));
                text.Span(header.Text(DocumentStrings.Page) + " ");
                text.CurrentPageNumber();
                text.Span(" " + header.Text(DocumentStrings.Of) + " ");
                text.TotalPages();
            });
        });

    /// <summary>"Billed to / Received from / Supplier" with the party's name and contact lines.</summary>
    public static void PartyBox(IContainer container, string caption, string name, List<string?> lines) =>
        container.Border(RuleWidth).BorderColor(RuleColor).Padding(6).Column(col =>
        {
            col.Item().Text(caption).FontSize(SmallFontSize).FontColor(Muted);
            col.Item().PaddingTop(1).Text(name).FontSize(BodyFontSize + 2).Bold();

            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    col.Item().PaddingTop(1).Text(line).FontSize(BodyFontSize);
                }
            }
        });

    /// <summary>The document's number, date and similar details as a two-column block.</summary>
    public static void Fields(IContainer container, List<DocumentField> fields) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(30, Unit.Millimetre);
                c.RelativeColumn();
            });

            foreach (var field in fields)
            {
                table.Cell().PaddingVertical(1.5f).Text(field.Label).FontSize(SmallFontSize + 0.5f).FontColor(Muted);
                table.Cell().PaddingVertical(1.5f).AlignRight().Text(field.Value).SemiBold();
            }
        });

    public static IContainer HeadCell(IContainer cell) =>
        cell.Background(HeaderFill).BorderBottom(1).BorderColor(Colors.Black)
            .PaddingVertical(CellPaddingVertical).PaddingHorizontal(CellPaddingHorizontal);

    // ShowEntire keeps a wrapped cell from being split across a page break.
    public static IContainer BodyCell(IContainer cell) =>
        cell.ShowEntire().BorderBottom(RuleWidth).BorderColor(RuleColor)
            .PaddingVertical(CellPaddingVertical).PaddingHorizontal(CellPaddingHorizontal);

    public static void HeadText(IContainer cell, string text, bool alignRight = false)
    {
        var container = HeadCell(cell);
        (alignRight ? container.AlignRight() : container.AlignLeft()).Text(text).FontSize(SmallFontSize + 0.5f).Bold();
    }

    public static void BodyText(IContainer cell, string text, bool alignRight = false)
    {
        var container = BodyCell(cell);
        (alignRight ? container.AlignRight() : container.AlignLeft()).Text(text);
    }

    /// <summary>The right-hand totals box (Total, Received, Balance due…).</summary>
    public static void Totals(IContainer container, List<DocumentTotal> rows) =>
        container.Border(RuleWidth).BorderColor(RuleColor).Column(col =>
        {
            foreach (var total in rows)
            {
                var item = col.Item();
                if (total.Emphasis)
                {
                    item = item.BorderTop(1).BorderColor(Colors.Black).Background(HeaderFill);
                }

                item.PaddingVertical(3).PaddingHorizontal(5).Row(row =>
                {
                    var label = row.RelativeItem().Text(total.Label);
                    var value = row.AutoItem().AlignRight().Text(total.Value);

                    if (total.Emphasis)
                    {
                        label.Bold().FontSize(BodyFontSize + 1);
                        value.Bold().FontSize(BodyFontSize + 1);
                    }
                    else
                    {
                        value.SemiBold();
                    }
                });
            }
        });

    /// <summary>"In words: Taka Twelve Thousand Only".</summary>
    public static void InWords(IContainer container, DocumentHeader header, string words) =>
        container.Column(col =>
        {
            col.Item().Text(header.Text(DocumentStrings.AmountInWords)).FontSize(SmallFontSize).FontColor(Muted);
            col.Item().Text(words).SemiBold();
        });

    /// <summary>Payments already received, oldest first.</summary>
    public static void Payments(IContainer container, DocumentHeader header, LocalizedText caption, IReadOnlyList<DocumentPaymentLine> payments) =>
        container.Column(col =>
        {
            col.Item().PaddingBottom(3).Text(header.Text(caption)).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(28, Unit.Millimetre);
                    c.ConstantColumn(30, Unit.Millimetre);
                    c.RelativeColumn();
                    c.ConstantColumn(32, Unit.Millimetre);
                });

                table.Header(h =>
                {
                    HeadText(h.Cell(), header.Text(DocumentStrings.Date));
                    HeadText(h.Cell(), header.Text(DocumentStrings.Method));
                    HeadText(h.Cell(), header.Text(DocumentStrings.Reference));
                    HeadText(h.Cell(), header.Text(DocumentStrings.AmountBdt), alignRight: true);
                });

                foreach (var payment in payments)
                {
                    BodyText(table.Cell(), header.Date(payment.Date));
                    BodyText(table.Cell(), payment.Method);
                    BodyText(table.Cell(), payment.Reference ?? string.Empty);
                    BodyText(table.Cell(), header.Money(payment.AmountBdt), alignRight: true);
                }
            });
        });

    /// <summary>A boxed note (terms, remarks).</summary>
    public static void NoteBox(IContainer container, string caption, string text) =>
        container.Border(RuleWidth).BorderColor(RuleColor).Padding(6).Column(col =>
        {
            col.Item().Text(caption).FontSize(SmallFontSize).Bold();
            col.Item().PaddingTop(1).Text(text).FontSize(SmallFontSize + 0.5f);
        });

    /// <summary>Signature lines, spread across the page width.</summary>
    public static void Signatures(IContainer container, DocumentHeader header, List<LocalizedText> roles) =>
        container.PaddingTop(34).Row(row =>
        {
            for (var i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (i > 0)
                {
                    row.ConstantItem(30, Unit.Millimetre);
                }

                row.RelativeItem().Column(col =>
                {
                    col.Item().BorderTop(RuleWidth).BorderColor(Colors.Black);
                    col.Item().PaddingTop(2).AlignCenter().Text(header.Text(role)).FontSize(SmallFontSize + 0.5f);
                });
            }
        });

    /// <summary>The closing line under the signatures.</summary>
    public static void Closing(IContainer container, string text) =>
        container.PaddingTop(14).AlignCenter().Text(text).FontSize(BodyFontSize).FontColor(Muted);
}
