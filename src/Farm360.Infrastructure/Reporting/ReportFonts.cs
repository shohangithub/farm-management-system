using System;
using System.IO;
using System.Reflection;
using QuestPDF.Drawing;
using Farm360.Domain.BusinessRules;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Reporting;

/// <summary>
/// Registers the report typefaces with QuestPDF exactly once per process.
/// </summary>
/// <remarks>
/// The fonts are embedded resources rather than machine-installed fonts on purpose: the API runs
/// in a Linux container where <c>C:\Windows\Fonts</c> does not exist, and a report that silently
/// falls back to a font without Bengali glyphs prints boxes instead of numbers a farmer can read.
/// Noto Sans Bengali is used under the SIL Open Font License 1.1 (see Fonts/OFL.txt), which
/// permits redistribution inside an application.
/// </remarks>
public static class ReportFonts
{
    /// <summary>Primary family. Covers Bengali and Latin, so most reports need nothing else.</summary>
    public const string Primary = "Noto Sans Bengali";

    /// <summary>Fallback for glyphs Noto Sans Bengali does not carry (QuestPDF ships Lato).</summary>
    public const string Fallback = "Lato";

    private static readonly object Gate = new();
    private static bool _initialised;

    /// <summary>
    /// Registers fonts and declares the QuestPDF licence tier.
    /// </summary>
    /// <param name="licence">
    /// From <c>Farm360:BusinessRules:PdfLicence</c>. Community is free only while annual revenue
    /// is below USD 1M — a fact only the business can confirm, which is why this is configuration
    /// rather than a constant. Crossing the threshold means buying a licence and changing the
    /// setting; no code changes.
    /// </param>
    /// <param name="licenceKey">Required for the paid tiers.</param>
    public static void EnsureRegistered(
        PdfLicenceMode licence = PdfLicenceMode.Community,
        string? licenceKey = null)
    {
        if (_initialised)
        {
            return;
        }

        lock (Gate)
        {
            if (_initialised)
            {
                return;
            }

            QuestPDF.Settings.License = licence switch
            {
                PdfLicenceMode.Professional => LicenseType.Professional,
                PdfLicenceMode.Enterprise => LicenseType.Enterprise,
                _ => LicenseType.Community,
            };

            if (licence != PdfLicenceMode.Community && string.IsNullOrWhiteSpace(licenceKey))
            {
                throw new InvalidOperationException(
                    $"Farm360:BusinessRules:PdfLicence is '{licence}' but no PdfLicenceKey was configured.");
            }

            // Report layout must be identical on every machine, so never let the host's installed
            // fonts influence it — only what we ship is allowed to resolve.
            QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

            Register("NotoSansBengali-Regular.ttf");
            Register("NotoSansBengali-Bold.ttf");

            _initialised = true;
        }
    }

    private static void Register(string fileName)
    {
        var assembly = typeof(ReportFonts).GetTypeInfo().Assembly;
        var resourceName = $"Farm360.Infrastructure.Reporting.Fonts.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded report font '{resourceName}' is missing. " +
                "Check the EmbeddedResource entry in Farm360.Infrastructure.csproj.");

        // FontManager needs a seekable stream it can read fully.
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        EnsureLooksLikeFont(buffer.ToArray(), resourceName);

        buffer.Position = 0;
        FontManager.RegisterFontFromStream(buffer);
    }

    /// <summary>
    /// Checks the sfnt signature before handing the bytes to QuestPDF.
    /// </summary>
    /// <remarks>
    /// A font fetched over HTTP can quietly be a 404 page of exactly plausible size; the renderer's
    /// own error ("cannot load the provided font data") does not say which file or why. Failing here
    /// names the resource, which turns a confusing runtime crash into an obvious build-asset problem.
    /// </remarks>
    private static void EnsureLooksLikeFont(byte[] data, string resourceName)
    {
        // 0x00010000 = TrueType outlines · "OTTO" = CFF · "true"/"ttcf" = Apple / collection.
        var valid = data.Length > 4
            && ((data[0] == 0x00 && data[1] == 0x01 && data[2] == 0x00 && data[3] == 0x00)
                || IsTag(data, "OTTO") || IsTag(data, "true") || IsTag(data, "ttcf"));

        if (!valid)
        {
            throw new InvalidOperationException(
                $"Embedded report font '{resourceName}' is not a valid font file " +
                $"(first bytes: {Convert.ToHexString(data, 0, Math.Min(4, data.Length))}, length: {data.Length}).");
        }
    }

    private static bool IsTag(byte[] data, string tag) =>
        data[0] == tag[0] && data[1] == tag[1] && data[2] == tag[2] && data[3] == tag[3];
}
