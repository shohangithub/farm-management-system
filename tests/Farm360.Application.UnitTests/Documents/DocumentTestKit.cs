using System;
using Farm360.Application.Documents;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.UnitTests.Documents;

internal static class DocumentTestKit
{
    /// <summary>A valid 1×1 PNG — enough to exercise the logo path of the letterhead.</summary>
    public static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    public static DocumentHeader Header(
        ReportLanguage language = ReportLanguage.En,
        bool bengaliNumerals = false,
        byte[]? logo = null) =>
        new(
            "Rahman Agro",
            "Green Valley Farm",
            "House 12, Road 4, Savar, Dhaka · 01711-000000 · info@rahmanagro.com",
            logo,
            language,
            bengaliNumerals,
            "Rafiq Ahmed (Manager)",
            "24-Nov-2026 10:00 BST (UTC+6)",
            new DateOnly(2026, 11, 24));
}
