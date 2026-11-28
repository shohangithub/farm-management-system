using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Health.Queries.SpecializedReports;
using Farm360.Application.Livestock.Queries;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Livestock.Enums;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One health event of any kind, flattened so they can share a single chronological band.</summary>
public sealed record AnimalHealthReportRow(
    DateOnly Date,
    string EventType,
    string Title,
    string Detail,
    string Provider,
    decimal? CostBdt,
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
    private readonly Farm360.Application.Finance.Repositories.IFinancialTransactionRepository _transactions;

    public AnimalHealthReportDefinition(Farm360.Application.Finance.Repositories.IFinancialTransactionRepository transactions)
    {
        _transactions = transactions;
    }

    public override string Key => "livestock.animal-health";

    public override LocalizedText Title => new("Animal Health History", "পশুর স্বাস্থ্য ইতিহাস");

    public override LocalizedText Description => new(
        "What illnesses, treatments and vaccinations has this animal had, and what did they cost? One line per event, month by month.",
        "পশুটির কী কী রোগ, চিকিৎসা ও টিকা হয়েছে এবং খরচ কত? প্রতিটি ঘটনা এক লাইনে, মাস অনুযায়ী।");

    public override ReportCategory Category => ReportCategory.Health;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "current-year"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalHealthReportRow>> Columns =>
    [
        ReportColumn<AnimalHealthReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<AnimalHealthReportRow>.Text("eventType", new LocalizedText("Event", "ঘটনা"), r => r.EventType, width: 24f, relative: false),
        ReportColumn<AnimalHealthReportRow>.Text("title", new LocalizedText("Illness / vaccine", "রোগ / টিকা"), r => r.Title, width: 2.2f),
        ReportColumn<AnimalHealthReportRow>.Text("detail", new LocalizedText("Medicine, dose & details", "ঔষধ, মাত্রা ও বিবরণ"), r => r.Detail, width: 3.2f),
        ReportColumn<AnimalHealthReportRow>.Text("provider", new LocalizedText("Vet", "পশুচিকিৎসক"), r => r.Provider, width: 1.6f),
        ReportColumn<AnimalHealthReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 26f, relative: false),
        ReportColumn<AnimalHealthReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<AnimalHealthReportRow>? Group =>
        ReportGroup<AnimalHealthReportRow>.ByLocalized(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => new LocalizedText(
                $"Month: {r.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}",
                $"মাস: {BanglaMonths[r.Date.Month - 1]} {r.Date.Year.ToString(CultureInfo.InvariantCulture)}"));

    public override LocalizedText? GrandTotalLabel => new("Total health cost for the period", "সময়কালের মোট স্বাস্থ্য খরচ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one health event: a treatment, a vaccination or a reported illness, in date order and grouped by month.",
            "প্রতিটি সারি একটি স্বাস্থ্য ঘটনা: চিকিৎসা, টিকা বা রোগের খবর; তারিখ অনুযায়ী সাজানো ও মাস অনুযায়ী ভাগ করা।"),
        new("Treatment cost is the amount entered on the treatment; vaccination cost is the medicine booked against that vaccination.",
            "চিকিৎসার খরচ চিকিৎসার সাথে লেখা পরিমাণ; টিকার খরচ সেই টিকার জন্য হিসাবে তোলা ঔষধের দাম।"),
        new("Illness reports carry no cost of their own (the treatment that follows does), so their cost cell is blank.",
            "রোগের খবরের নিজস্ব কোনো খরচ নেই (পরের চিকিৎসায় খরচ ধরা হয়), তাই খরচের ঘর ফাঁকা।"),
        new("Where a medicine has a withdrawal period, do not sell the milk or meat for the days shown.",
            "যে ঔষধের প্রত্যাহারকাল আছে, দেখানো দিনগুলোতে দুধ বা মাংস বিক্রি করবেন না।"),
    ];

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

        var bn = context.Language == ReportLanguage.Bn;
        var period = context.Range("period");
        var age = AnimalAge.Format(animal.DateOfBirth, animal.ExitDate ?? context.Today);
        return $"{(bn ? "ট্যাগ" : "Tag")} {AnimalLabels.Tag(animal.TagId, animal.Status, animal.ExitDate, context.Language, context.BengaliNumerals)} · {SpeciesLabel(animal.Species, bn)} · {SexLabel(animal.Sex, bn)} · {(bn ? "বয়স" : "age")} {age} · "
            + $"{period.From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {period.To.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }

    protected override async Task<IReadOnlyList<AnimalHealthReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var period = context.Range("period");
        var bn = context.Language == ReportLanguage.Bn;

        var data = await context
            .Send(new GetAnimalHealthReportQuery(animalId), cancellationToken)
            .ConfigureAwait(false);

        // What each vaccination actually cost: the medicine posting the stock deduction (or the
        // direct cash booking when stock ran out) made against that vaccination event.
        var vaccinationCosts = await LoadVaccinationCostsAsync(animalId, cancellationToken).ConfigureAwait(false);

        var rows = new List<AnimalHealthReportRow>(
            data.Treatments.Count + data.Vaccinations.Count + data.DiseaseIncidents.Count);

        foreach (var t in data.Treatments)
        {
            if (!period.Contains(t.StartDate))
            {
                continue;
            }

            var dosage = $"{t.MedicationName}: {t.DosageAmount.ToString("0.##", CultureInfo.InvariantCulture)} {t.DosageUnit}";

            // Withdrawal periods belong on the face of the report: selling milk or meat inside
            // one is a food-safety breach, not a footnote.
            if (t.MilkWithdrawalDays > 0 || t.MeatWithdrawalDays > 0)
            {
                dosage += bn
                    ? $" (দুধ বিক্রি বন্ধ {t.MilkWithdrawalDays} দিন, মাংস {t.MeatWithdrawalDays} দিন)"
                    : $" (no milk sale for {t.MilkWithdrawalDays} days, no meat sale for {t.MeatWithdrawalDays} days)";
            }

            rows.Add(new AnimalHealthReportRow(
                t.StartDate,
                bn ? "চিকিৎসা" : "Treatment",
                string.IsNullOrWhiteSpace(t.Diagnosis) ? "—" : t.Diagnosis,
                dosage,
                string.IsNullOrWhiteSpace(t.VeterinarianName) ? "—" : t.VeterinarianName,
                t.CostBdt,
                TreatmentStatusLabel(t.Status, bn)));
        }

        foreach (var v in data.Vaccinations)
        {
            var date = v.AdministeredDate ?? v.ScheduledDate;
            if (!period.Contains(date))
            {
                continue;
            }

            var detail = string.IsNullOrWhiteSpace(v.BatchNumber) ? "—" : (bn ? $"ব্যাচ নম্বর {v.BatchNumber}" : $"Vaccine batch {v.BatchNumber}");

            rows.Add(new AnimalHealthReportRow(
                date,
                bn ? "টিকা" : "Vaccination",
                v.VaccineName,
                v.AdministeredDate is null
                    ? (bn ? $"{detail} (নির্ধারিত তারিখ {v.ScheduledDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)})" : $"{detail} (due {v.ScheduledDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)})")
                    : detail,
                "—",
                vaccinationCosts.TryGetValue(v.Id, out var vaccineCost) ? vaccineCost : null,
                VaccinationStatusLabel(v.Status, bn)));
        }

        foreach (var i in data.DiseaseIncidents)
        {
            if (!period.Contains(i.IncidentDate))
            {
                continue;
            }

            rows.Add(new AnimalHealthReportRow(
                i.IncidentDate,
                bn ? "অসুস্থতা" : "Illness",
                i.DiseaseName,
                $"{(string.IsNullOrWhiteSpace(i.Symptoms) ? "—" : i.Symptoms)} · {(bn ? "তীব্রতা" : "Severity")}: {SeverityLabel(i.Severity, bn)}",
                "—",
                null,
                IncidentStatusLabel(i.Status, bn)));
        }

        // Chronological, then by type, so the group band stays contiguous and the reader
        // follows the animal's history in the order it happened.
        return rows
            .OrderBy(r => r.Date)
            .ThenBy(r => r.EventType, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly string[] BanglaMonths =
    [
        "জানুয়ারি", "ফেব্রুয়ারি", "মার্চ", "এপ্রিল", "মে", "জুন",
        "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর",
    ];

    private static string SpeciesLabel(AnimalSpecies species, bool bn) => species switch
    {
        AnimalSpecies.CattleBeef => bn ? "মাংসের গরু" : "Beef cattle",
        AnimalSpecies.CattleDairy => bn ? "দুগ্ধ গরু" : "Dairy cattle",
        AnimalSpecies.Goat => bn ? "ছাগল" : "Goat",
        AnimalSpecies.Sheep => bn ? "ভেড়া" : "Sheep",
        _ => species.ToString(),
    };

    private static string SexLabel(AnimalSex sex, bool bn) => sex switch
    {
        AnimalSex.Male => bn ? "পুরুষ" : "Male",
        AnimalSex.Female => bn ? "স্ত্রী" : "Female",
        _ => sex.ToString(),
    };

    private static string TreatmentStatusLabel(TreatmentStatus status, bool bn) => status switch
    {
        TreatmentStatus.Ongoing => bn ? "চলছে" : "Ongoing",
        TreatmentStatus.Completed => bn ? "সম্পন্ন" : "Completed",
        TreatmentStatus.Failed => bn ? "কাজ করেনি" : "Did not work",
        TreatmentStatus.Referred => bn ? "অন্যত্র পাঠানো" : "Referred",
        _ => status.ToString(),
    };

    private static string VaccinationStatusLabel(VaccinationStatus status, bool bn) => status switch
    {
        VaccinationStatus.Scheduled => bn ? "নির্ধারিত" : "Scheduled",
        VaccinationStatus.Completed => bn ? "দেওয়া হয়েছে" : "Given",
        VaccinationStatus.Overdue => bn ? "সময় পেরিয়েছে" : "Overdue",
        VaccinationStatus.Cancelled => bn ? "বাতিল" : "Cancelled",
        _ => status.ToString(),
    };

    private static string IncidentStatusLabel(IncidentStatus status, bool bn) => status switch
    {
        IncidentStatus.Reported => bn ? "জানানো হয়েছে" : "Reported",
        IncidentStatus.UnderTreatment => bn ? "চিকিৎসাধীন" : "Under treatment",
        IncidentStatus.Contained => bn ? "নিয়ন্ত্রণে" : "Contained",
        IncidentStatus.Resolved => bn ? "সেরে গেছে" : "Recovered",
        _ => status.ToString(),
    };

    private static string SeverityLabel(IncidentSeverity severity, bool bn) => severity switch
    {
        IncidentSeverity.Mild => bn ? "হালকা" : "Mild",
        IncidentSeverity.Moderate => bn ? "মাঝারি" : "Moderate",
        IncidentSeverity.Severe => bn ? "গুরুতর" : "Severe",
        IncidentSeverity.Critical => bn ? "সংকটজনক" : "Critical",
        _ => severity.ToString(),
    };

    private async Task<Dictionary<Guid, decimal>> LoadVaccinationCostsAsync(Guid animalId, CancellationToken cancellationToken)
    {
        var transactions = await _transactions.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        var costs = new Dictionary<Guid, decimal>();

        foreach (var t in transactions)
        {
            if (t.Category != Farm360.Domain.Finance.Enums.TransactionCategory.MedicineCost)
            {
                continue;
            }

            var eventId = t.SourceDocumentId
                ?? (Guid.TryParse(t.ReferenceId, out var parsed) ? parsed : (Guid?)null);

            if (eventId is { } id)
            {
                costs[id] = costs.GetValueOrDefault(id) + t.AmountBdt;
            }
        }

        return costs;
    }
}
