using System.Globalization;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Reporting.Definitions;

/// <summary>How reports name an animal: its tag, plus "(Sold 12-Nov-2026)" / "(Died …)" once it has
/// left the farm, so history rows for a sold or dead animal are never mistaken for a live one. In a
/// Bangla report the suffix is Bangla too: "(বিক্রি ১২ নভেম্বর ২০২৬)".</summary>
public static class AnimalLabels
{
    private static readonly string[] BanglaMonths =
    [
        "জানুয়ারি", "ফেব্রুয়ারি", "মার্চ", "এপ্রিল", "মে", "জুন",
        "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর",
    ];

    /// <summary>The tag with an English suffix (kept for callers without a report context).</summary>
    public static string Tag(Animal animal) => Tag(animal, ReportLanguage.En, bengaliNumerals: false);

    /// <summary>The tag with the suffix in the report's language and digits.</summary>
    public static string Tag(Animal animal, ReportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Tag(animal, context.Language, context.BengaliNumerals);
    }

    public static string Tag(Animal animal, ReportLanguage language, bool bengaliNumerals)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return animal.HasLeftFarm
            ? Tag(animal.Tag.TagId, animal.Status, animal.ExitDate, language, bengaliNumerals, leftFallback: true)
            : animal.Tag.TagId;
    }

    /// <summary>
    /// "B-04", or "B-04 (Sold 12-Nov-2026)" / "B-04 (বিক্রি ১২ নভেম্বর ২০২৬)" once the animal has
    /// left the farm. <paramref name="leftFallback"/> labels an unrecognised exit status "Left".
    /// </summary>
    public static string Tag(string tagId, AnimalStatus status, DateOnly? exitDate, ReportLanguage language,
        bool bengaliNumerals, bool leftFallback = false)
    {
        var bn = language == ReportLanguage.Bn;
        var what = status switch
        {
            AnimalStatus.Sold => bn ? "বিক্রি" : "Sold",
            AnimalStatus.Dead => bn ? "মৃত্যু" : "Died",
            AnimalStatus.Slaughtered => bn ? "জবাই" : "Slaughtered",
            AnimalStatus.Transferred => bn ? "খামার ছেড়েছে" : "Left",
            _ => leftFallback ? (bn ? "খামার ছেড়েছে" : "Left") : null,
        };

        if (what is null)
            return tagId;

        return exitDate is { } d ? $"{tagId} ({what} {Day(d, language, bengaliNumerals)})" : $"{tagId} ({what})";
    }

    /// <summary>"12-Nov-2026" in English; "১২ নভেম্বর ২০২৬" in Bangla (digits per the farm setting).</summary>
    public static string Day(DateOnly date, ReportLanguage language, bool bengaliNumerals)
    {
        var text = language == ReportLanguage.Bn
            ? $"{date.Day.ToString(CultureInfo.InvariantCulture)} {BanglaMonths[date.Month - 1]} {date.Year.ToString(CultureInfo.InvariantCulture)}"
            : date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture);
        return bengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(text) : text;
    }
}
