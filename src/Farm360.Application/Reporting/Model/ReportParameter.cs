using System;
using System.Collections.Generic;

namespace Farm360.Application.Reporting.Model;

public enum ReportParameterType
{
    Text = 0,
    WholeNumber = 1,
    Number = 2,
    Boolean = 3,
    Date = 4,
    DateRange = 5,
    Select = 6,
    MultiSelect = 7,
    Animal = 8,
    Batch = 9,
    Farm = 10,
    Shed = 11,
    Breed = 12,
}

public sealed record ReportSelectOption(string Value, LocalizedText Label);

/// <summary>
/// One declared input of a report. The Angular parameter panel renders any report from this
/// schema alone — there is no per-report form to hand-write.
/// </summary>
public sealed record ReportParameter(
    string Name,
    LocalizedText Label,
    ReportParameterType Type,
    bool Required = false,
    string? DefaultValue = null,
    IReadOnlyList<ReportSelectOption>? Options = null,
    string? HelpText = null)
{
    public static ReportParameter Animal(string name = "animalId", bool required = true) =>
        new(name, new LocalizedText("Animal", "পশু"), ReportParameterType.Animal, required);

    public static ReportParameter Batch(string name = "batchId", bool required = false) =>
        new(name, new LocalizedText("Batch", "ব্যাচ"), ReportParameterType.Batch, required);

    public static ReportParameter Farm(string name = "farmId", bool required = false) =>
        new(name, new LocalizedText("Farm", "খামার"), ReportParameterType.Farm, required);

    /// <summary>
    /// A from/to pair. The client sends "yyyy-MM-dd..yyyy-MM-dd"; <see cref="ReportContext.Range"/>
    /// parses it. Default may be a preset keyword ("current-month", "last-30-days", "current-year").
    /// </summary>
    public static ReportParameter DateRange(string name = "period", string? @default = "current-month", bool required = true) =>
        new(name, new LocalizedText("Period", "সময়কাল"), ReportParameterType.DateRange, required, @default);

    public static ReportParameter Date(string name, LocalizedText label, bool required = true, string? @default = "today") =>
        new(name, label, ReportParameterType.Date, required, @default);

    public static ReportParameter Bool(string name, LocalizedText label, bool @default = false) =>
        new(name, label, ReportParameterType.Boolean, false, @default ? "true" : "false");

    public static ReportParameter Select(string name, LocalizedText label, IReadOnlyList<ReportSelectOption> options, string? @default = null, bool required = false) =>
        new(name, label, ReportParameterType.Select, required, @default, options);

    public static ReportParameter Number(string name, LocalizedText label, decimal? @default = null, bool required = false) =>
        new(name, label, ReportParameterType.Number, required, @default?.ToString(System.Globalization.CultureInfo.InvariantCulture));
}

/// <summary>Inclusive date range. Reports are period documents; this is the unit they work in.</summary>
public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    public bool Contains(DateOnly d) => d >= From && d <= To;

    public override string ToString() => $"{From:dd-MMM-yyyy} to {To:dd-MMM-yyyy}";

    public static DateRange CurrentMonth(DateOnly today) =>
        new(new DateOnly(today.Year, today.Month, 1),
            new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month)));

    public static DateRange LastDays(DateOnly today, int days) => new(today.AddDays(-(days - 1)), today);

    public static DateRange CurrentYear(DateOnly today) =>
        new(new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31));
}
