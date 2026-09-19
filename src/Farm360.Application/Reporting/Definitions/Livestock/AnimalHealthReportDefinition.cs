using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Health.Queries.SpecializedReports;
using Farm360.Application.Livestock.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One health event of any kind, flattened so they can share a single chronological band.</summary>
public sealed record AnimalHealthReportRow(
    DateOnly Date,
    string EventType,
    string Title,
    string Detail,
    string Provider,
    decimal CostBdt,
    string Status);

/// <summary>
/// Monthly medical history for one animal: treatments, vaccinations and disease incidents
/// interleaved in date order and sub-totalled by month.
/// </summary>
/// <remarks>
/// Deliberately one merged band rather than three separate tables. A vet or an owner reading an
/// animal's history wants the sequence — treated on the 3rd, vaccinated on the 11th, relapsed on
/// the 20th — and three tables side by side hide exactly that.
/// </remarks>
public sealed class AnimalHealthReportDefinition : ReportDefinition<AnimalHealthReportRow>
{
    public override string Key => "livestock.animal-health";

    public override LocalizedText Title => new("Animal Medical & Health Report", "পশুর চিকিৎসা ও স্বাস্থ্য প্রতিবেদন");

    public override LocalizedText Description => new(
        "Treatments, vaccinations and disease incidents for one animal, by month.",
        "একটি পশুর চিকিৎসা, টিকা ও রোগের ঘটনা, মাসভিত্তিক।");

    public override ReportCategory Category => ReportCategory.Health;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalHealthReportRow>> Columns =>
    [
        ReportColumn<AnimalHealthReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<AnimalHealthReportRow>.Text("eventType", new LocalizedText("Type", "ধরন"), r => r.EventType, width: 22f, relative: false),
        ReportColumn<AnimalHealthReportRow>.Text("title", new LocalizedText("Diagnosis / Vaccine", "রোগ / টিকা"), r => r.Title, width: 2.2f),
        ReportColumn<AnimalHealthReportRow>.Text("detail", new LocalizedText("Medication / Detail", "ঔষধ / বিবরণ"), r => r.Detail, width: 2.4f),
        ReportColumn<AnimalHealthReportRow>.Text("provider", new LocalizedText("Veterinarian", "পশুচিকিৎসক"), r => r.Provider, width: 1.6f),
        ReportColumn<AnimalHealthReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 20f, relative: false),
        ReportColumn<AnimalHealthReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt),
    ];

    public override ReportGroup<AnimalHealthReportRow>? Group =>
        ReportGroup<AnimalHealthReportRow>.By(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => r.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await context
            .Send(new GetAnimalByIdQuery(context.Id("animalId")), cancellationToken)
            .ConfigureAwait(false);

        if (animal is null)
        {
            return null;
        }

        var age = (context.Today.DayNumber - animal.DateOfBirth.DayNumber) / 30;
        return $"Tag {animal.TagId} · {animal.Species} · {animal.Sex} · {age} months · {animal.Status}";
    }

    protected override async Task<IReadOnlyList<AnimalHealthReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var period = context.Range("period");

        var data = await context
            .Send(new GetAnimalHealthReportQuery(animalId), cancellationToken)
            .ConfigureAwait(false);

        var rows = new List<AnimalHealthReportRow>(
            data.Treatments.Count + data.Vaccinations.Count + data.DiseaseIncidents.Count);

        foreach (var t in data.Treatments)
        {
            if (!period.Contains(t.StartDate))
            {
                continue;
            }

            var dosage = $"{t.MedicationName} — {t.DosageAmount.ToString("0.##", CultureInfo.InvariantCulture)} {t.DosageUnit}";

            // Withdrawal periods belong on the face of the report: selling milk or meat inside
            // one is a food-safety breach, not a footnote.
            if (t.MilkWithdrawalDays > 0 || t.MeatWithdrawalDays > 0)
            {
                dosage += $" (withdrawal: milk {t.MilkWithdrawalDays}d, meat {t.MeatWithdrawalDays}d)";
            }

            rows.Add(new AnimalHealthReportRow(
                t.StartDate,
                "Treatment",
                t.Diagnosis,
                dosage,
                t.VeterinarianName ?? "—",
                t.CostBdt,
                t.Status.ToString()));
        }

        foreach (var v in data.Vaccinations)
        {
            var date = v.AdministeredDate ?? v.ScheduledDate;
            if (!period.Contains(date))
            {
                continue;
            }

            var detail = string.IsNullOrWhiteSpace(v.BatchNumber) ? "—" : $"Batch {v.BatchNumber}";

            rows.Add(new AnimalHealthReportRow(
                date,
                "Vaccination",
                v.VaccineName,
                v.AdministeredDate is null ? $"{detail} (scheduled {v.ScheduledDate:dd-MMM-yyyy})" : detail,
                "—",

                // Vaccination cost is not modelled per event yet; showing 0 keeps the column
                // honest and the month sub-total equal to the treatment spend.
                0m,
                v.Status.ToString()));
        }

        foreach (var i in data.DiseaseIncidents)
        {
            if (!period.Contains(i.IncidentDate))
            {
                continue;
            }

            rows.Add(new AnimalHealthReportRow(
                i.IncidentDate,
                "Incident",
                i.DiseaseName,
                i.Symptoms,
                $"Severity: {i.Severity}",
                0m,
                i.Status.ToString()));
        }

        // Chronological, then by type, so the group band stays contiguous and the reader
        // follows the animal's history in the order it happened.
        return rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.EventType, StringComparer.Ordinal)
            .ToList();
    }
}
