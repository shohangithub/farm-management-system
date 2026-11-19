using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Health;

/// <summary>Where a treated animal stands against its medicine's withdrawal periods on a day.</summary>
/// <param name="LastDoseDate">The last day medicine was given; null while a course is still running with no end date.</param>
/// <param name="MeatClearDate">First day the animal may be sold for meat; null while the course is running.</param>
/// <param name="MilkClearDate">First day its milk may be used; null while the course is running.</param>
public sealed record WithdrawalPosition(
    DateOnly? LastDoseDate,
    DateOnly? MeatClearDate,
    DateOnly? MilkClearDate,
    bool MeatWithheld,
    bool MilkWithheld)
{
    public bool IsWithheld => MeatWithheld || MilkWithheld;
}

/// <summary>The withdrawal rule, public so it is tested rather than described.</summary>
public static class Withdrawal
{
    /// <summary>
    /// Withdrawal runs from the <b>last</b> dose: the end date of the course, or the start date of a
    /// finished single-day treatment. A course still running with no end date has not started its
    /// withdrawal yet, so the animal is withheld for every period the medicine has. The animal is
    /// clear on (last dose + days); before that day it may not be sold for meat / its milk used.
    /// </summary>
    public static WithdrawalPosition Evaluate(
        DateOnly startDate,
        DateOnly? endDate,
        TreatmentStatus status,
        int meatDays,
        int milkDays,
        DateOnly asOf)
    {
        DateOnly? lastDose = endDate ?? (status == TreatmentStatus.Ongoing ? null : startDate);

        if (lastDose is not { } last)
        {
            return new WithdrawalPosition(null, null, null, meatDays > 0, milkDays > 0);
        }

        var meatClear = last.AddDays(Math.Max(0, meatDays));
        var milkClear = last.AddDays(Math.Max(0, milkDays));
        return new WithdrawalPosition(
            last,
            meatClear,
            milkClear,
            meatDays > 0 && asOf < meatClear,
            milkDays > 0 && asOf < milkClear);
    }
}

public sealed record TreatmentWithdrawalReportRow(
    string Section,
    string Tag,
    DateOnly StartDate,
    string Diagnosis,
    string Medication,
    decimal DoseAmount,
    string DoseUnit,
    DateOnly? LastDoseDate,
    DateOnly? MeatClearDate,
    DateOnly? MilkClearDate,
    string Status,
    decimal? CostBdt);

/// <summary>
/// Report H6 — treatment &amp; withdrawal register: every treatment started in the period with its
/// medicine, dose and the dates the animal's meat and milk are clear; animals on the farm that are
/// inside a withdrawal period today are listed first, whenever their treatment began.
/// </summary>
/// <remarks>
/// <para>
/// Withdrawal is counted from the last dose (see <see cref="Withdrawal.Evaluate"/>), which is the
/// food-safety rule; a still-running course keeps the animal withheld until it ends.
/// </para>
/// <para>
/// Cost is printed only for treatments started in the period, so the cost total is the period's
/// treatment cost even though section A can include older treatments still under withdrawal.
/// Doses are totalled only within one unit.
/// </para>
/// </remarks>
public sealed class TreatmentWithdrawalReportDefinition : ReportDefinition<TreatmentWithdrawalReportRow>
{
    /// <summary>How far back to look for treatments whose withdrawal may still be running.</summary>
    private const int WithdrawalLookBackDays = 365;

    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IAnimalRepository _animals;

    public TreatmentWithdrawalReportDefinition(IMedicalTreatmentRepository treatments, IAnimalRepository animals)
    {
        _treatments = treatments;
        _animals = animals;
    }

    public override string Key => "health.treatment-withdrawal";

    public override LocalizedText Title => new("Treatment & Withdrawal Register", "চিকিৎসা ও প্রত্যাহারকাল রেজিস্টার");

    public override LocalizedText Description => new(
        "Treatments in the period with medicine, dose and meat/milk withdrawal end dates; animals currently within a withdrawal period (no sale, no milk) listed first.",
        "নির্বাচিত সময়ের চিকিৎসা — ঔষধ, মাত্রা এবং মাংস/দুধের প্রত্যাহারকাল শেষের তারিখ; বর্তমানে প্রত্যাহারকালে থাকা পশু (বিক্রয় ও দুধ নিষেধ) সবার আগে।");

    public override ReportCategory Category => ReportCategory.Health;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total (treatments started in the period)", "মোট (নির্বাচিত সময়ে শুরু হওয়া চিকিৎসা)");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<TreatmentWithdrawalReportRow>> Columns =>
    [
        ReportColumn<TreatmentWithdrawalReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.Tag, width: 22f, relative: false),
        ReportColumn<TreatmentWithdrawalReportRow>.Date("start", new LocalizedText("Started", "শুরু"), r => r.StartDate),
        ReportColumn<TreatmentWithdrawalReportRow>.Text("diagnosis", new LocalizedText("Diagnosis", "রোগনির্ণয়"), r => r.Diagnosis, width: 1.6f),
        ReportColumn<TreatmentWithdrawalReportRow>.Text("medication", new LocalizedText("Medicine", "ঔষধ"), r => r.Medication, width: 1.6f),
        ReportColumn<TreatmentWithdrawalReportRow>.Number("dose", new LocalizedText("Dose", "মাত্রা"), r => r.DoseAmount, 2, 16f, ReportAggregate.Sum, r => r.DoseUnit),
        ReportColumn<TreatmentWithdrawalReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.DoseUnit, width: 12f, relative: false),
        ReportColumn<TreatmentWithdrawalReportRow>.Date("lastDose", new LocalizedText("Last dose", "শেষ মাত্রা"), r => r.LastDoseDate),
        ReportColumn<TreatmentWithdrawalReportRow>.Date("meatClear", new LocalizedText("Meat clear on", "মাংস নিরাপদ"), r => r.MeatClearDate),
        ReportColumn<TreatmentWithdrawalReportRow>.Date("milkClear", new LocalizedText("Milk clear on", "দুধ নিরাপদ"), r => r.MilkClearDate),
        ReportColumn<TreatmentWithdrawalReportRow>.Text("status", new LocalizedText("Withdrawal status", "প্রত্যাহারকালের অবস্থা"), r => r.Status, width: 2f),
        ReportColumn<TreatmentWithdrawalReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, widthMm: 22f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<TreatmentWithdrawalReportRow>? Group =>
        ReportGroup<TreatmentWithdrawalReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string N(int v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals);

        return context.Language == ReportLanguage.Bn
            ? $"আজ প্রত্যাহারকালে {N(data.WithheldAnimals)} টি পশু (বিক্রয়/দুধ নিষেধ) · এই সময়ে {N(data.TreatmentsInPeriod)} টি চিকিৎসা"
            : $"{N(data.WithheldAnimals)} animals within a withdrawal period today (no sale / no milk) · {N(data.TreatmentsInPeriod)} treatments in the period";
    }

    protected override async Task<IReadOnlyList<TreatmentWithdrawalReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Rows;
    }

    private sealed record RegisterData(IReadOnlyList<TreatmentWithdrawalReportRow> Rows, int WithheldAnimals, int TreatmentsInPeriod);

    private Task<RegisterData> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("treatment-withdrawal", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var today = context.Today;
            var bn = context.Language == ReportLanguage.Bn;
            string T(string en, string bnText) => bn ? bnText : en;

            var lookFrom = period.From < today.AddDays(-WithdrawalLookBackDays) ? period.From : today.AddDays(-WithdrawalLookBackDays);
            var lookTo = period.To > today ? period.To : today;

            var treatments = await _treatments.GetByFarmStartedBetweenAsync(farmId, lookFrom, lookTo, cancellationToken).ConfigureAwait(false);
            var animals = (await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false))
                .ToDictionary(a => a.Id);

            var withheldSection = T("A. On the farm and within withdrawal today — do not sell, do not use milk", "ক. আজ প্রত্যাহারকালে থাকা পশু — বিক্রয় ও দুধ ব্যবহার নিষেধ");
            var periodSection = T("B. Other treatments started in the period", "খ. নির্বাচিত সময়ে শুরু হওয়া অন্যান্য চিকিৎসা");

            var rows = new List<TreatmentWithdrawalReportRow>();
            var withheldAnimals = new HashSet<Guid>();
            var inPeriodCount = 0;

            foreach (var t in treatments)
            {
                animals.TryGetValue(t.AnimalId, out var animal);
                var onFarm = animal is not null && animal.IsOnFarmOn(today);
                var inPeriod = period.Contains(t.StartDate);
                var position = Withdrawal.Evaluate(t.StartDate, t.EndDate, t.Status, t.WithdrawalPeriod.MeatDays, t.WithdrawalPeriod.MilkDays, today);
                var withheld = onFarm && position.IsWithheld;

                if (!withheld && !inPeriod)
                {
                    continue;
                }

                if (inPeriod)
                {
                    inPeriodCount++;
                }

                if (withheld)
                {
                    withheldAnimals.Add(t.AnimalId);
                }

                rows.Add(new TreatmentWithdrawalReportRow(
                    withheld ? withheldSection : periodSection,
                    animal?.Tag.TagId ?? "—",
                    t.StartDate,
                    t.Diagnosis,
                    t.MedicationName,
                    t.Dosage.Amount,
                    t.Dosage.Unit,
                    position.LastDoseDate,
                    t.WithdrawalPeriod.MeatDays > 0 ? position.MeatClearDate : null,
                    t.WithdrawalPeriod.MilkDays > 0 ? position.MilkClearDate : null,
                    StatusText(position, animal, onFarm, bn),
                    inPeriod ? t.CostBdt : null));
            }

            IReadOnlyList<TreatmentWithdrawalReportRow> ordered = rows
                .OrderBy(r => r.Section == withheldSection ? 0 : 1)
                .ThenBy(r => r.Section == withheldSection ? (r.MeatClearDate ?? r.MilkClearDate ?? DateOnly.MaxValue) : r.StartDate)
                .ThenBy(r => r.Tag, StringComparer.Ordinal)
                .ToList();

            return new RegisterData(ordered, withheldAnimals.Count, inPeriodCount);
        });

    private static string StatusText(WithdrawalPosition p, Animal? animal, bool onFarm, bool bn)
    {
        static string D(DateOnly? d) => d?.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture) ?? string.Empty;

        if (!onFarm)
        {
            return animal is null
                ? (bn ? "পশু পাওয়া যায়নি" : "Animal not found")
                : (bn ? "খামারে নেই" : "No longer on the farm");
        }

        if (p.LastDoseDate is null && p.IsWithheld)
        {
            return bn ? "চিকিৎসা চলছে — বিক্রয়/দুধ নিষেধ" : "Under treatment — no sale / no milk";
        }

        if (p.MeatWithheld && p.MilkWithheld)
        {
            return bn ? $"বিক্রয় নিষেধ {D(p.MeatClearDate)} পর্যন্ত; দুধ নিষেধ {D(p.MilkClearDate)} পর্যন্ত" : $"No sale before {D(p.MeatClearDate)}; no milk before {D(p.MilkClearDate)}";
        }

        if (p.MeatWithheld)
        {
            return bn ? $"বিক্রয় নিষেধ {D(p.MeatClearDate)} পর্যন্ত" : $"No sale before {D(p.MeatClearDate)}";
        }

        if (p.MilkWithheld)
        {
            return bn ? $"দুধ নিষেধ {D(p.MilkClearDate)} পর্যন্ত" : $"No milk before {D(p.MilkClearDate)}";
        }

        return bn ? "নিরাপদ" : "Clear";
    }
}
