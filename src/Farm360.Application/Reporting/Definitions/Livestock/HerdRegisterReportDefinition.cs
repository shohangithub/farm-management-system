using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

public sealed record HerdRegisterReportRow(
    string TagId,
    string Batch,
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
    private readonly IBreedRepository _breeds;
    private readonly IAnimalBatchRepository _batches;

    public HerdRegisterReportDefinition(IAnimalRepository animals, IBreedRepository breeds, IAnimalBatchRepository batches)
    {
        _animals = animals;
        _breeds = breeds;
        _batches = batches;
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
        ReportParameter.Farm(required: true),
    ];

    public override IReadOnlyList<ReportColumn<HerdRegisterReportRow>> Columns =>
    [
        ReportColumn<HerdRegisterReportRow>.Text("tag", new LocalizedText("Tag ID", "ট্যাগ নম্বর"), r => r.TagId, width: 2.2f),
        // Animals carry no name in Farm360 — the tag is the identity — so the column that used to
        // print the free-text notes as a "name" now shows the batch the animal belongs to.
        ReportColumn<HerdRegisterReportRow>.Text("batch", new LocalizedText("Batch", "ব্যাচ"), r => r.Batch, width: 2.5f),
        ReportColumn<HerdRegisterReportRow>.Text("species", new LocalizedText("Species", "প্রজাতি"), r => r.Species, width: 1.8f),
        ReportColumn<HerdRegisterReportRow>.Text("breed", new LocalizedText("Breed", "জাত"), r => r.Breed, width: 2.2f),
        ReportColumn<HerdRegisterReportRow>.Text("sex", new LocalizedText("Sex", "লিঙ্গ"), r => r.Sex, width: 16f, relative: false),
        ReportColumn<HerdRegisterReportRow>.WholeNumber("age", new LocalizedText("Age (Months)", "বয়স (মাস)"), r => r.AgeMonths, widthMm: 22f),
        ReportColumn<HerdRegisterReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, decimals: 1, widthMm: 24f),
        ReportColumn<HerdRegisterReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 20f, relative: false),
    ];

    public override ReportGroup<HerdRegisterReportRow>? Group =>
        ReportGroup<HerdRegisterReportRow>.ByLocalized(
            r => r.Status,
            r => new LocalizedText($"Status: {r.Status}", $"অবস্থা: {r.Status}"));

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

        var farmId = context.FarmId();

        // Fetches every page rather than just the first 5000 -- see HerdSummaryReportDefinition
        // for why: a farm past that size would otherwise get a silently incomplete register with
        // no indication rows were missing.
        const int pageSize = 5000;
        var items = new List<Farm360.Domain.Livestock.Animal>();
        var pageNumber = 1;
        while (true)
        {
            var (page, totalCount) = await _animals
                .GetPagedAsync(pageNumber, pageSize, farmId: farmId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // The count arrives with the first page: refuse an oversized register before loading the rest.
            context.EnsureWithinRowLimit(totalCount, Key);

            items.AddRange(page);
            if (items.Count >= totalCount || page.Count == 0)
            {
                break;
            }

            pageNumber++;
        }

        if (items.Count == 0)
        {
            return [];
        }

        var today = context.Today;

        // Breeds are a small master list (one query); batches are few per farm (one per batch).
        var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(b => b.Id, b => b.Name);
        var batches = new Dictionary<Guid, string>();
        foreach (var batchId in items.Where(a => a.BatchId.HasValue).Select(a => a.BatchId!.Value).Distinct())
        {
            var batch = await _batches.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false);
            if (batch is not null)
            {
                batches[batchId] = batch.Name;
            }
        }

        return items
            .Select(a =>
            {
                var ageMonths = AnimalAge.CompletedMonths(a.DateOfBirth, today);
                return new HerdRegisterReportRow(
                    TagId: a.Tag.TagId,
                    Batch: a.BatchId is { } b && batches.TryGetValue(b, out var batchName) ? batchName : "—",
                    Species: a.Species.ToString(),
                    Breed: breeds.TryGetValue(a.BreedId, out var breedName) ? breedName : "Native / unspecified",
                    Sex: a.Sex.ToString(),
                    AgeMonths: ageMonths,
                    WeightKg: a.LatestWeightKg ?? 0m,
                    Status: a.Status.ToString());
            })
            .OrderBy(r => r.Status)
            .ThenBy(r => r.TagId)
            .ToList();
    }
}
