using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Health.Queries.VaccinationEvents;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Health;

public sealed record VaccinationComplianceReportRow(
    string VaccineName,
    string AnimalTag,
    DateOnly ScheduledDate,
    DateOnly? AdministeredDate,
    string Status,
    string Notes);

/// <summary>
/// Report H4 — Vaccination schedule, upcoming doses, overdue interventions, and herd compliance rate.
/// </summary>
public sealed class VaccinationComplianceReportDefinition : ReportDefinition<VaccinationComplianceReportRow>
{
    private readonly IAnimalRepository _animals;

    public VaccinationComplianceReportDefinition(IAnimalRepository animals)
    {
        _animals = animals;
    }

    public override string Key => "health.vaccination-compliance";

    public override LocalizedText Title => new("Vaccination Due & Compliance Report", "টিকা প্রদান ও মান্যতা প্রতিবেদন");

    public override LocalizedText Description => new(
        "Overdue, upcoming, and administered herd vaccinations with schedules, vaccine names, and compliance tracking.",
        "খামারের গবাদিপশুর বকেয়া, আসন্ন ও সম্পন্নকৃত টিকার সময়সূচী এবং স্বাস্থ্য মান্যতা ট্র্যাকিং প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Health;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
        ReportParameter.Date("beforeDate", new LocalizedText("Schedule Horizon", "সময়সীমা"), required: false, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<VaccinationComplianceReportRow>> Columns =>
    [
        ReportColumn<VaccinationComplianceReportRow>.Text("vaccine", new LocalizedText("Vaccine / Medicine", "টিকা / প্রতিষেধক"), r => r.VaccineName, width: 2.8f),
        ReportColumn<VaccinationComplianceReportRow>.Text("tag", new LocalizedText("Animal Tag", "পশুর ট্যাগ"), r => r.AnimalTag, width: 2.0f),
        ReportColumn<VaccinationComplianceReportRow>.Date("scheduled", new LocalizedText("Due Date", "নির্ধারিত তারিখ"), r => r.ScheduledDate),
        ReportColumn<VaccinationComplianceReportRow>.Date("administered", new LocalizedText("Administered", "প্রদানের তারিখ"), r => r.AdministeredDate ?? DateOnly.MinValue),
        ReportColumn<VaccinationComplianceReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 20f, relative: false),
        ReportColumn<VaccinationComplianceReportRow>.Text("notes", new LocalizedText("Notes / Batch", "মন্তব্য / ব্যাচ"), r => r.Notes, width: 2.5f),
    ];

    public override ReportGroup<VaccinationComplianceReportRow>? Group =>
        ReportGroup<VaccinationComplianceReportRow>.By(
            r => r.Status,
            r => $"Status: {r.Status}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return "Herd Immunization & Preventive Health Compliance";
    }

    protected override async Task<IReadOnlyList<VaccinationComplianceReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var beforeDate = context.Date("beforeDate").AddMonths(3);

        var events = await context
            .Send(new GetUpcomingVaccinationsQuery(farmId, beforeDate), cancellationToken)
            .ConfigureAwait(false);

        if (events == null || events.Count == 0)
        {
            return [];
        }

        var animalIds = events.Select(e => e.AnimalId).Distinct().ToList();
        var animals = await _animals.GetByIdsAsync(animalIds, cancellationToken).ConfigureAwait(false);
        var animalDict = animals.ToDictionary(a => a.Id);

        return events
            .Select(e =>
            {
                var tag = animalDict.TryGetValue(e.AnimalId, out var a) ? a.Tag.TagId : e.AnimalId.ToString()[..8];
                return new VaccinationComplianceReportRow(
                    VaccineName: e.VaccineName,
                    AnimalTag: tag,
                    ScheduledDate: e.ScheduledDate,
                    AdministeredDate: e.AdministeredDate,
                    Status: e.Status.ToString(),
                    Notes: string.IsNullOrWhiteSpace(e.Notes) ? (e.BatchNumber ?? "—") : e.Notes);
            })
            .OrderBy(r => r.Status)
            .ThenBy(r => r.ScheduledDate)
            .ToList();
    }

    private static async Task<Guid> ResolveFarmIdAsync(ReportContext context, CancellationToken cancellationToken)
    {
        if (context.IdOrNull("farmId") is { } id && id != Guid.Empty)
            return id;
        if (context.AssignedFarmIds is { Count: > 0 } assigned && assigned[0] != Guid.Empty)
            return assigned[0];

        var farms = await context.Send(new GetAllFarmsQuery(), cancellationToken).ConfigureAwait(false);
        return farms.Count > 0 ? farms[0].Id : Guid.Empty;
    }
}
