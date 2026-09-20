using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

public sealed record HerdRegisterReportRow(
    string TagId,
    string Name,
    string Species,
    string Breed,
    string Sex,
    int AgeMonths,
    decimal WeightKg,
    string Status);

/// <summary>
/// Report H1 — Comprehensive Herd Register master list of all cattle in inventory.
/// </summary>
public sealed class HerdRegisterReportDefinition : ReportDefinition<HerdRegisterReportRow>
{
    private readonly IAnimalRepository _animals;

    public HerdRegisterReportDefinition(IAnimalRepository animals)
    {
        _animals = animals;
    }

    public override string Key => "livestock.herd-register";

    public override LocalizedText Title => new("Herd Register Master Report", "পাল নিবন্ধন প্রধান প্রতিবেদন");

    public override LocalizedText Description => new(
        "Complete herd inventory master list detailing tag, breed, sex, age, weight, and status.",
        "খামারের সকল গবাদিপশুর তালিকা, ট্যাগ, জাত, লিঙ্গ, বয়স, ওজন ও বর্তমান অবস্থা সম্বলিত প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(),
    ];

    public override IReadOnlyList<ReportColumn<HerdRegisterReportRow>> Columns =>
    [
        ReportColumn<HerdRegisterReportRow>.Text("tag", new LocalizedText("Tag ID", "ট্যাগ নম্বর"), r => r.TagId, width: 2.2f),
        ReportColumn<HerdRegisterReportRow>.Text("name", new LocalizedText("Animal Name", "নাম"), r => r.Name, width: 2.5f),
        ReportColumn<HerdRegisterReportRow>.Text("species", new LocalizedText("Species", "প্রজাতি"), r => r.Species, width: 1.8f),
        ReportColumn<HerdRegisterReportRow>.Text("breed", new LocalizedText("Breed", "জাত"), r => r.Breed, width: 2.2f),
        ReportColumn<HerdRegisterReportRow>.Text("sex", new LocalizedText("Sex", "লিঙ্গ"), r => r.Sex, width: 16f, relative: false),
        ReportColumn<HerdRegisterReportRow>.WholeNumber("age", new LocalizedText("Age (Months)", "বয়স (মাস)"), r => r.AgeMonths, widthMm: 22f),
        ReportColumn<HerdRegisterReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, decimals: 1, widthMm: 24f),
        ReportColumn<HerdRegisterReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 20f, relative: false),
    ];

    public override ReportGroup<HerdRegisterReportRow>? Group =>
        ReportGroup<HerdRegisterReportRow>.By(
            r => r.Status,
            r => $"Status: {r.Status}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return "Complete Herd Inventory Master Register";
    }

    protected override async Task<IReadOnlyList<HerdRegisterReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);

        var (items, _) = await _animals
            .GetPagedAsync(1, 5000, farmId: farmId == Guid.Empty ? null : farmId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (items.Count == 0)
        {
            return [];
        }

        var today = context.Today;

        return items
            .Select(a =>
            {
                var ageMonths = Math.Max(0, (today.DayNumber - a.DateOfBirth.DayNumber) / 30);
                return new HerdRegisterReportRow(
                    TagId: a.Tag.TagId,
                    Name: string.IsNullOrWhiteSpace(a.Notes) ? a.Tag.TagId : a.Notes,
                    Species: a.Species.ToString(),
                    Breed: a.BreedId != Guid.Empty ? a.BreedId.ToString()[..8] : "Native",
                    Sex: a.Sex.ToString(),
                    AgeMonths: ageMonths,
                    WeightKg: a.LatestWeightKg ?? 0m,
                    Status: a.Status.ToString());
            })
            .OrderBy(r => r.Status)
            .ThenBy(r => r.TagId)
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
