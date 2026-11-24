using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Reporting.Pdf;

/// <summary>What the letterhead prints: who the organisation is and how to reach it.</summary>
internal sealed record LetterheadContent(
    string OrganizationName,
    string? FarmName,
    string? ContactLine,
    byte[]? Logo);

/// <summary>
/// The one letterhead every Farm360 PDF carries — reports and customer documents alike: the logo
/// on the left, the organisation, farm and contact line in the centre, then the document's own
/// title block, all over a single black rule. Printer-friendly: black text, no fills.
/// </summary>
internal static class LetterheadComposer
{
    private const float OrgFontSize = 13f;
    private const float SmallFontSize = 8f;
    private const float LogoColumnMm = 24f;
    private const float LogoMaxHeightMm = 18f;

    private static readonly Color Muted = Colors.Grey.Darken1;

    /// <param name="container">Where to draw.</param>
    /// <param name="content">Organisation name, farm, contact line and logo.</param>
    /// <param name="titleBlock">The document's centred title lines (title, subject, number…).</param>
    public static void Compose(IContainer container, LetterheadContent content, Action<ColumnDescriptor> titleBlock)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(titleBlock);

        var logo = IsSupportedImage(content.Logo) ? content.Logo : null;

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                if (logo is not null)
                {
                    byte[] logoBytes = logo;

                    // A fixed column on each side keeps the centre — and the title — truly centred.
                    row.ConstantItem(LogoColumnMm, Unit.Millimetre)
                        .AlignLeft().AlignMiddle()
                        .Element(c => TryPlaceLogo(c, logoBytes));
                }

                row.RelativeItem().Column(centre =>
                {
                    centre.Item().AlignCenter().Text(content.OrganizationName).FontSize(OrgFontSize).Bold();

                    if (!string.IsNullOrWhiteSpace(content.FarmName))
                    {
                        centre.Item().AlignCenter().Text(content.FarmName!).FontSize(SmallFontSize);
                    }

                    if (!string.IsNullOrWhiteSpace(content.ContactLine))
                    {
                        centre.Item().AlignCenter().Text(content.ContactLine!).FontSize(SmallFontSize - 0.5f).FontColor(Muted);
                    }

                    titleBlock(centre);
                });

                if (logo is not null)
                {
                    row.ConstantItem(LogoColumnMm, Unit.Millimetre);
                }
            });

            col.Item().PaddingTop(3).BorderBottom(1).BorderColor(Colors.Black);
        });
    }

    /// <summary>
    /// Places the logo, or nothing. The bytes were validated as an image when uploaded, but a
    /// truncated or swapped file must cost the page its logo, never the whole document.
    /// </summary>
    private static void TryPlaceLogo(IContainer container, byte[] logo)
    {
        try
        {
            container.MaxHeight(LogoMaxHeightMm, Unit.Millimetre).Image(logo).FitArea();
        }
        catch (Exception)
        {
            // Undecodable image: print without it.
        }
    }

    /// <summary>PNG, JPEG or WebP by magic bytes — the formats the upload validator accepts.</summary>
    internal static bool IsSupportedImage(byte[]? data)
    {
        if (data is null || data.Length < 12)
        {
            return false;
        }

        var isJpeg = data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
        var isPng = data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
        var isWebp = data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F'
            && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P';

        return isJpeg || isPng || isWebp;
    }
}
