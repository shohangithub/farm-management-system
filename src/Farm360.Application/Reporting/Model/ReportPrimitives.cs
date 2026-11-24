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

    /// <summary>
    /// The generation time in the farm's own time zone, already formatted with the zone label —
    /// e.g. "16-Nov-2026 14:05 BST (UTC+6)". A farmer in Dhaka reading "08:05 UTC" has to do
    /// arithmetic to know when the page was printed; this is the line every renderer prints.
    /// </summary>
    public string? GeneratedAtLocal { get; init; }

    /// <summary>Short zone label, e.g. "BST (UTC+6)".</summary>
    public string? TimeZoneLabel { get; init; }

    /// <summary>Generation time in the farm's local zone, for file names and "as at" lines.</summary>
    public DateTime? GeneratedAtLocalTime { get; init; }

    /// <summary>
    /// The organisation's contact line printed under its name — address, phone, e-mail, whichever
    /// are on file — e.g. "Savar, Dhaka · 01711-000000 · info@rahmanagro.com".
    /// </summary>
    public string? ContactLine { get; init; }

    /// <summary>Relative URL of the organisation logo, for the on-screen paper.</summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// The logo itself, for the PDF letterhead. Never serialised: the on-screen viewer loads
    /// <see cref="LogoUrl"/> instead of receiving the image inside every dataset.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public byte[]? Logo { get; init; }

    /// <summary>The local generation stamp if resolved, otherwise the UTC one, explicitly labelled.</summary>
    public string GeneratedAtDisplay =>
        GeneratedAtLocal ?? GeneratedAtUtc.ToString("dd-MMM-yyyy HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    public static ReportMetaHeader Empty { get; } = new(
        "Farm360 AI", null, string.Empty, string.Empty, Guid.Empty, "system", DateTime.UtcNow, []);
}

public sealed record ReportMetaField(string Label, string Value);

/// <summary>
/// Every fixed string the platform itself prints (as opposed to a definition's own labels), in
/// both report languages. Renderers take these from here so a Bengali report never carries an
/// English "GRAND TOTAL" band.
/// </summary>
public static class ReportStrings
{
    public static LocalizedText SubTotal { get; } = new("Sub-total", "উপ-মোট");
    public static LocalizedText GrandTotal { get; } = new("Grand total", "সর্বমোট");
    public static LocalizedText Rows { get; } = new("rows", "সারি");
    public static LocalizedText HowToRead { get; } = new("How to read this report", "প্রতিবেদনটি যেভাবে পড়বেন");
    public static LocalizedText NoData { get; } = new(
        "No data available for the selected criteria.",
        "নির্বাচিত শর্তে কোনো তথ্য পাওয়া যায়নি।");
    public static LocalizedText GeneratedBy { get; } = new("Generated by", "প্রস্তুতকারী");
    public static LocalizedText GeneratedAt { get; } = new("Generated at", "প্রস্তুতের সময়");
    public static LocalizedText Generated { get; } = new("Generated", "প্রস্তুত");
    public static LocalizedText Run { get; } = new("run", "রান");
    public static LocalizedText Page { get; } = new("Page", "পৃষ্ঠা");
    public static LocalizedText Of { get; } = new("of", "এর");
    public static LocalizedText Group { get; } = new("Group", "গ্রুপ");
    public static LocalizedText RowType { get; } = new("Row type", "সারির ধরন");
    public static LocalizedText Detail { get; } = new("Detail", "বিস্তারিত");
    public static LocalizedText All { get; } = new("All", "সকল");
    public static LocalizedText Yes { get; } = new("Yes", "হ্যাঁ");
    public static LocalizedText No { get; } = new("No", "না");
    public static LocalizedText Farm { get; } = new("Farm", "খামার");
    public static LocalizedText PreparedBy { get; } = new("Prepared by", "প্রস্তুতকারী");
    public static LocalizedText CheckedBy { get; } = new("Checked by", "যাচাইকারী");
    public static LocalizedText ApprovedBy { get; } = new("Approved by", "অনুমোদনকারী");
}
