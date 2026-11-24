using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;

namespace Farm360.Application.Reporting.Definitions.Health;

/// <summary>Where one dose stands, from the farm's point of view.</summary>
public enum VaccinationComplianceStatus
{
    GivenOnTime = 0,
    GivenLate = 1,
    Due = 2,
    Overdue = 3,
    Cancelled = 4,
}

public sealed record VaccinationComplianceReportRow(
    string Protocol,
    string VaccineName,
    string AnimalTag,
    DateOnly DueDate,
    DateOnly? GivenDate,
    VaccinationComplianceStatus Status,
    string StatusLabel,
    int? DaysLate,
    string Notes)
{
    public int Given => Status is VaccinationComplianceStatus.GivenOnTime or VaccinationComplianceStatus.GivenLate ? 1 : 0;
    public int OnTime => Status == VaccinationComplianceStatus.GivenOnTime ? 1 : 0;
    public int Pending => Status == VaccinationComplianceStatus.Due ? 1 : 0;
    public int Overdue => Status == VaccinationComplianceStatus.Overdue ? 1 : 0;
    public int Cancelled => Status == VaccinationComplianceStatus.Cancelled ? 1 : 0;
}

/// <summary>Doses given on time ÷ doses that fell due in the period (cancelled doses excluded).</summary>
public sealed record VaccinationComplianceSummary(int DueInPeriod, int GivenOnTime, int Overdue, int Upcoming)
{
    public decimal? CompliancePercent => DueInPeriod == 0 ? null : decimal.Round(GivenOnTime * 100m / DueInPeriod, 1);
}

/// <summary>
/// Report H4 — vaccination compliance per protocol for animals on the farm: given (on time or
/// late), due, overdue and cancelled doses in an explicit period, with a compliance percentage.
/// </summary>
/// <remarks>
/// <para>
/// The earlier version listed only still-scheduled doses (so nothing given ever appeared), printed
/// "01-Jan-0001" as the administered date of every row, and silently pushed the horizon three
/// months past the date the user chose. This one reads every status, leaves the given date blank
/// when there is none, and reports exactly the period asked for.
/// </para>
/// <para>
/// A dose is "on time" when given no later than its due date plus the grace period. Compliance is
/// measured only over doses whose due date has already arrived — a booster due next week is not
/// a failure today.
/// </para>
/// </remarks>
public sealed class VaccinationComplianceReportDefinition : ReportDefinition<VaccinationComplianceReportRow>
{
    private const int DefaultGraceDays = 7;

    private readonly IVaccinationRepository _vaccinations;

    public VaccinationComplianceReportDefinition(IVaccinationRepository vaccinations)
    {
        _vaccinations = vaccinations;
    }

    public override string Key => "health.vaccination-compliance";

    public override LocalizedText Title => new("Vaccinations: Given, Due & Overdue", "টিকা: দেওয়া, বাকি ও সময় পেরোনো");

    public override LocalizedText Description => new(
        "Are our animals vaccinated on time? Every dose due in the period, by vaccine schedule: given on time, given late, still to give or overdue, with the on-time rate.",
        "আমাদের পশুরা কি সময়মতো টিকা পাচ্ছে? নির্বাচিত সময়ে প্রতিটি টিকার মাত্রা, টিকা সূচি অনুযায়ী: সময়মতো দেওয়া, দেরিতে দেওয়া, বাকি বা সময় পেরোনো, এবং সময়মতো দেওয়ার হার।");

    public override ReportCategory Category => ReportCategory.Health;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("All vaccines", "সকল টিকা");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-month", required: true),
        ReportParameter.Number(
            "graceDays",
            new LocalizedText("Grace period (days) for 'on time'", "সময়মতো ধরার অতিরিক্ত দিন"),
            @default: DefaultGraceDays),
    ];

    public override IReadOnlyList<ReportColumn<VaccinationComplianceReportRow>> Columns =>
    [
        ReportColumn<VaccinationComplianceReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.AnimalTag, width: 1.6f),
        ReportColumn<VaccinationComplianceReportRow>.Text("vaccine", new LocalizedText("Vaccine", "টিকা"), r => r.VaccineName, width: 2.2f),
        ReportColumn<VaccinationComplianceReportRow>.Date("due", new LocalizedText("Due date", "নির্ধারিত তারিখ"), r => r.DueDate),
        ReportColumn<VaccinationComplianceReportRow>.Date("given", new LocalizedText("Given on", "দেওয়ার তারিখ"), r => r.GivenDate),
        ReportColumn<VaccinationComplianceReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.StatusLabel, width: 26f, relative: false),
        ReportColumn<VaccinationComplianceReportRow>.WholeNumber("daysLate", new LocalizedText("Days late", "দেরি (দিন)"), r => r.DaysLate, widthMm: 18f),
        ReportColumn<VaccinationComplianceReportRow>.WholeNumber("givenCount", new LocalizedText("Given (doses)", "দেওয়া হয়েছে (মাত্রা)"), r => r.Given, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<VaccinationComplianceReportRow>.WholeNumber("onTimeCount", new LocalizedText("On time (doses)", "সময়মতো (মাত্রা)"), r => r.OnTime, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<VaccinationComplianceReportRow>.WholeNumber("dueCount", new LocalizedText("Still to give (doses)", "এখনো বাকি (মাত্রা)"), r => r.Pending, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<VaccinationComplianceReportRow>.WholeNumber("overdueCount", new LocalizedText("Overdue (doses)", "সময় পেরিয়েছে (মাত্রা)"), r => r.Overdue, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<VaccinationComplianceReportRow>.Text("notes", new LocalizedText("Notes / vaccine batch", "মন্তব্য / টিকার ব্যাচ"), r => r.Notes, width: 2.0f),
    ];

    public override ReportGroup<VaccinationComplianceReportRow>? Group =>
        ReportGroup<VaccinationComplianceReportRow>.ByLocalized(
            r => r.Protocol,
            r => new LocalizedText($"Vaccine schedule: {r.Protocol}", $"টিকা সূচি: {r.Protocol}"));

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one dose for one animal on the farm, grouped by vaccine schedule; the count columns are 1 or 0 so each group shows how many doses are in each state.",
            "প্রতিটি সারি খামারে থাকা একটি পশুর একটি টিকার মাত্রা, টিকা সূচি অনুযায়ী ভাগ করা; গণনার ঘরগুলো ১ বা ০, তাই প্রতিটি গ্রুপে কোন অবস্থায় কতটি মাত্রা তা দেখা যায়।"),
        new("A dose is on time if given no later than the due date plus the grace days chosen for this report.",
            "নির্ধারিত তারিখ + এই প্রতিবেদনের জন্য বাছাই করা অতিরিক্ত দিনের মধ্যে দিলে মাত্রাটি সময়মতো ধরা হয়।"),
        new("On-time rate (in the line above the table) = doses given on time ÷ doses whose due date has already come; doses due later are not counted as missed.",
            "সময়মতো দেওয়ার হার (টেবিলের উপরের লাইনে) = সময়মতো দেওয়া মাত্রা ÷ যে মাত্রার নির্ধারিত তারিখ এসে গেছে; পরে নির্ধারিত মাত্রা বাদ পড়া ধরা হয় না।"),
        new("Days late: for a given dose, days after the due date it was given; for an overdue dose, days since it fell due. Cancelled doses are listed but not counted.",
            "দেরি (দিন): দেওয়া মাত্রার ক্ষেত্রে নির্ধারিত তারিখের কত দিন পর দেওয়া হয়েছে; সময় পেরোনো মাত্রার ক্ষেত্রে নির্ধারিত তারিখ থেকে কত দিন গেছে। বাতিল মাত্রা দেখানো হয় কিন্তু গণনা হয় না।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var period = context.Range("period");
        var rows = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var s = Summarise(rows, period, context.Today);
        var bn = context.Language == ReportLanguage.Bn;

        string N(decimal value, int decimals = 0) => ReportValueFormatter.Format(
            value, decimals == 0 ? ReportColumnType.WholeNumber : ReportColumnType.Number, decimals, context.BengaliNumerals);

        var range = $"{period.From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {period.To.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

        if (s.CompliancePercent is not { } pct)
        {
            return bn
                ? $"{range} · এই সময়ে কোনো টিকার তারিখ আসেনি · আসন্ন {N(s.Upcoming)} টি"
                : $"{range} · No doses fell due yet in the period · {N(s.Upcoming)} still to give";
        }

        return bn
            ? $"{range} · সময়মতো দেওয়ার হার {N(pct, 1)}% ({N(s.DueInPeriod)} টির মধ্যে {N(s.GivenOnTime)} টি) · সময় পেরিয়েছে {N(s.Overdue)} টি · বাকি {N(s.Upcoming)} টি"
            : $"{range} · On-time rate {N(pct, 1)}% ({N(s.GivenOnTime)} of {N(s.DueInPeriod)} doses due were given on time) · {N(s.Overdue)} overdue · {N(s.Upcoming)} still to give";
    }

    protected override Task<IReadOnlyList<VaccinationComplianceReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LoadAsync(context, cancellationToken);
    }

    /// <summary>Classifies one dose. Public so the rule is tested, not just described.</summary>
    public static VaccinationComplianceStatus Classify(
        VaccinationStatus status,
        DateOnly dueDate,
        DateOnly? givenDate,
        DateOnly today,
        int graceDays)
    {
        if (status == VaccinationStatus.Cancelled)
        {
            return VaccinationComplianceStatus.Cancelled;
        }

        if (givenDate is { } given)
        {
            return given <= dueDate.AddDays(Math.Max(0, graceDays))
                ? VaccinationComplianceStatus.GivenOnTime
                : VaccinationComplianceStatus.GivenLate;
        }

        if (status == VaccinationStatus.Completed)
        {
            // Completed without a recorded date: given, but its timeliness cannot be shown.
            return VaccinationComplianceStatus.GivenLate;
        }

        return dueDate < today ? VaccinationComplianceStatus.Overdue : VaccinationComplianceStatus.Due;
    }

    /// <summary>Compliance over doses whose due date is inside the period and not in the future.</summary>
    public static VaccinationComplianceSummary Summarise(
        IEnumerable<VaccinationComplianceReportRow> rows,
        DateRange period,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var dueInPeriod = 0;
        var onTime = 0;
        var overdue = 0;
        var upcoming = 0;

        foreach (var r in rows)
        {
            if (r.Status == VaccinationComplianceStatus.Cancelled)
            {
                continue;
            }

            if (r.Status == VaccinationComplianceStatus.Overdue)
            {
                overdue++;
            }

            if (r.Status == VaccinationComplianceStatus.Due)
            {
                upcoming++;
            }

            if (!period.Contains(r.DueDate) || r.DueDate > today)
            {
                continue;
            }

            dueInPeriod++;
            if (r.Status == VaccinationComplianceStatus.GivenOnTime)
            {
                onTime++;
            }
        }

        return new VaccinationComplianceSummary(dueInPeriod, onTime, overdue, upcoming);
    }

    private Task<IReadOnlyList<VaccinationComplianceReportRow>> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("vaccination-compliance", async () =>
        {
            var period = context.Range("period");
            var grace = context.WholeNumber("graceDays", DefaultGraceDays);
            if (grace is < 0 or > 90)
            {
                throw new ReportParameterException("graceDays", "Grace period must be between 0 and 90 days.");
            }

            var events = await _vaccinations
                .GetComplianceEventsAsync(context.FarmId(), period.From, period.To, cancellationToken)
                .ConfigureAwait(false);

            var noProtocol = context.Language == ReportLanguage.Bn ? "সূচির বাইরে (একক টিকা)" : "Not on a schedule (one-off)";

            IReadOnlyList<VaccinationComplianceReportRow> rows = events
                .Select(x =>
                {
                    var e = x.Event;
                    var status = Classify(e.Status, e.ScheduledDate, e.AdministeredDate, context.Today, grace);
                    int? daysLate = e.AdministeredDate is { } given
                        ? Math.Max(0, given.DayNumber - e.ScheduledDate.DayNumber)
                        : status == VaccinationComplianceStatus.Overdue
                            ? context.Today.DayNumber - e.ScheduledDate.DayNumber
                            : null;

                    return new VaccinationComplianceReportRow(
                        string.IsNullOrWhiteSpace(x.ProtocolTitle) ? noProtocol : x.ProtocolTitle!,
                        e.VaccineName,
                        x.AnimalTag,
                        e.ScheduledDate,
                        e.AdministeredDate,
                        status,
                        Label(status, context.Language),
                        daysLate,
                        string.IsNullOrWhiteSpace(e.Notes)
                            ? (string.IsNullOrWhiteSpace(e.BatchNumber) ? "—" : e.BatchNumber)
                            : e.Notes!);
                })
                .OrderBy(r => r.Protocol, StringComparer.Ordinal)
                .ThenBy(r => r.DueDate)
                .ThenBy(r => r.AnimalTag, StringComparer.Ordinal)
                .ToList();

            return rows;
        });

    private static string Label(VaccinationComplianceStatus status, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return status switch
        {
            VaccinationComplianceStatus.GivenOnTime => bn ? "সময়মতো দেওয়া" : "Given on time",
            VaccinationComplianceStatus.GivenLate => bn ? "দেরিতে দেওয়া" : "Given late",
            VaccinationComplianceStatus.Due => bn ? "এখনো বাকি" : "Still to give",
            VaccinationComplianceStatus.Overdue => bn ? "সময় পেরিয়েছে" : "Overdue",
            VaccinationComplianceStatus.Cancelled => bn ? "বাতিল" : "Cancelled",
            _ => status.ToString(),
        };
    }
}
