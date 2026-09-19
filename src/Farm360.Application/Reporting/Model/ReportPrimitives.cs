using System;
using System.Collections.Generic;

namespace Farm360.Application.Reporting.Model;

/// <summary>
/// A label in both languages the farm operates in. Every user-visible string in a report
/// definition carries both, so switching language never requires touching the definition.
/// </summary>
public sealed record LocalizedText(string En, string Bn)
{
    public static implicit operator LocalizedText(string en) => new(en, en);

    public string For(ReportLanguage language) =>
        language == ReportLanguage.Bn && !string.IsNullOrWhiteSpace(Bn) ? Bn : En;
}

public enum ReportLanguage
{
    En = 0,
    Bn = 1,
}

/// <summary>Catalog grouping in the Report Center. Mirrors the application's modules.</summary>
public enum ReportCategory
{
    Livestock = 0,
    Feeding = 1,
    Health = 2,
    Finance = 3,
    Inventory = 4,
    Intelligence = 5,
    Operations = 6,
}

/// <summary>
/// Drives formatting and alignment consistently across every renderer, so the on-screen
/// page, the PDF and the spreadsheet never disagree about what a number looks like.
/// </summary>
public enum ReportColumnType
{
    Text = 0,
    Date = 1,
    DateTime = 2,
    WholeNumber = 3,
    Number = 4,
    Money = 5,
    Percent = 6,
    Boolean = 7,
}

public enum ReportAlign
{
    Left = 0,
    Center = 1,
    Right = 2,
}

/// <summary>Column-footer aggregate, computed by the platform — never hand-written per report.</summary>
public enum ReportAggregate
{
    None = 0,
    Sum = 1,
    Average = 2,
    Count = 3,
    Min = 4,
    Max = 5,
}

public enum ReportOrientation
{
    Portrait = 0,
    Landscape = 1,
}

public enum ReportExportFormat
{
    Pdf = 0,
    Xlsx = 1,
    Csv = 2,
}

/// <summary>Page geometry. Defaults match the A4 house style in docs/32 §5.</summary>
public sealed record PageSetup(
    ReportOrientation Orientation = ReportOrientation.Portrait,
    float MarginMm = 10f,
    bool RepeatHeader = true,
    bool ShowPageNumbers = true,
    bool ShowSignatureBlock = false)
{
    public static PageSetup A4Portrait { get; } = new();

    public static PageSetup A4Landscape { get; } = new(ReportOrientation.Landscape);

    /// <summary>Financial and valuation documents are signed; management reports are not.</summary>
    public static PageSetup A4PortraitSigned { get; } = new(ShowSignatureBlock: true);

    public static PageSetup A4LandscapeSigned { get; } = new(ReportOrientation.Landscape, ShowSignatureBlock: true);
}

/// <summary>
/// The letterhead and parameter-echo content printed at the top of every report.
/// Built by the platform, never by a definition — that is what makes the pack consistent.
/// </summary>
public sealed record ReportMetaHeader(
    string OrganizationName,
    string? FarmName,
    string Title,
    string ReportKey,
    Guid RunId,
    string GeneratedByName,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ReportMetaField> Parameters)
{
    /// <summary>
    /// What the report is *about*, printed under the title — e.g. "Tag BD-0042 · Sahiwal · Female".
    /// Supplied by the definition, because only it knows its own subject.
    /// </summary>
    public string? Subject { get; init; }

    public static ReportMetaHeader Empty { get; } = new(
        "Farm360 AI", null, string.Empty, string.Empty, Guid.Empty, "system", DateTime.UtcNow, []);
}

public sealed record ReportMetaField(string Label, string Value);
