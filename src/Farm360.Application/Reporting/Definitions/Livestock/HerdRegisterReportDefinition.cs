using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

public sealed record HerdRegisterReportRow(
    string TagId,
    string Batch,
    string Species,
    string Breed,
    string Sex,
    int AgeMonths,
    decimal? WeightKg,
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

    public override LocalizedText Title => new("Herd Register", "পশু নিবন্ধন তালিকা");

    public override LocalizedText Description => new(
        "Which animals do we have, and which have left? Every animal on record with its tag, batch, breed, sex, age and latest weight, grouped by status.",
        "খামারে কোন কোন পশু আছে এবং কোনগুলো চলে গেছে? প্রতিটি পশুর ট্যাগ, ব্যাচ, জাত, লিঙ্গ, বয়স ও সর্বশেষ ওজন, অবস্থা অনুযায়ী ভাগ করা।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
    ];

    public override IReadOnlyList<ReportColumn<HerdRegisterReportRow>> Columns =>
    [
        ReportColumn<HerdRegisterReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.TagId, width: 2.6f),
        // Animals carry no name in Farm360 — the tag is the identity — so the column that used to
        // print the free-text notes as a "name" now shows the batch the animal belongs to.
        ReportColumn<HerdRegisterReportRow>.Text("batch", new LocalizedText("Batch", "ব্যাচ"), r => r.Batch, width: 2.2f),
        ReportColumn<HerdRegisterReportRow>.Text("species", new LocalizedText("Type of animal", "পশুর ধরন"), r => r.Species, width: 1.8f),
        ReportColumn<HerdRegisterReportRow>.Text("breed", new LocalizedText("Breed", "জাত"), r => r.Breed, width: 2.2f),
        ReportColumn<HerdRegisterReportRow>.Text("sex", new LocalizedText("Sex", "লিঙ্গ"), r => r.Sex, width: 18f, relative: false),
        ReportColumn<HerdRegisterReportRow>.WholeNumber("age", new LocalizedText("Age (months)", "বয়স (মাস)"), r => r.AgeMonths, widthMm: 22f),
        ReportColumn<HerdRegisterReportRow>.Number("weight", new LocalizedText("Latest weight (kg)", "সর্বশেষ ওজন (কেজি)"), r => r.WeightKg, decimals: 1, widthMm: 26f),
        ReportColumn<HerdRegisterReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 24f, relative: false),
    ];

    public override ReportGroup<HerdRegisterReportRow>? Group =>
        ReportGroup<HerdRegisterReportRow>.ByLocalized(
            r => r.Status,
            r => new LocalizedText($"Status: {r.Status}", $"অবস্থা: {r.Status}"));

    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one animal ever recorded on this farm; animals still on the farm come first, then those sold, slaughtered, dead or transferred.",
            "প্রতিটি সারি এই খামারে নিবন্ধিত একটি পশু; খামারে থাকা পশু আগে, তারপর বিক্রি, জবাই, মৃত বা স্থানান্তরিত পশু।"),
        new("Animals that have left show the date next to the tag, e.g. \"B-04 (Sold 12-Nov-2026)\".",
            "খামার ছেড়ে যাওয়া পশুর ট্যাগের পাশে তারিখ দেখানো আছে, যেমন \"B-04 (Sold 12-Nov-2026)\"।"),
        new("Age is in completed months and stops at the sale or death date.",
            "বয়স পূর্ণ মাসে, এবং বিক্রি বা মৃত্যুর তারিখে বয়স গণনা থেমে যায়।"),
        new("Latest weight is the most recent weigh-in; a blank cell means the animal has never been weighed.",
            "সর্বশেষ ওজন হলো শেষবার নেওয়া ওজন; ঘর ফাঁকা থাকলে পশুটির কখনো ওজন নেওয়া হয়নি।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var asOf = context.Today.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"নিবন্ধিত সকল পশু · {asOf} পর্যন্ত"
            : $"All animals on record · as of {asOf}");
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
        var bn = context.Language == ReportLanguage.Bn;

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

        // On-farm animals first (Active, Quarantined), then the ones that left, in enum order;
        // the group bands follow this order.
        return items
            .OrderBy(a => (int)a.Status)
            .ThenBy(a => a.Tag.TagId, StringComparer.OrdinalIgnoreCase)
            .Select(a =>
            {
                var ageMonths = AnimalAge.CompletedMonths(a.DateOfBirth, a.ExitDate ?? today);
                return new HerdRegisterReportRow(
                    TagId: AnimalLabels.Tag(a, context),
                    Batch: a.BatchId is { } b && batches.TryGetValue(b, out var batchName) ? batchName : (bn ? "ব্যাচ নেই" : "Not in a batch"),
                    Species: SpeciesLabel(a.Species, bn),
                    Breed: breeds.TryGetValue(a.BreedId, out var breedName) ? breedName : (bn ? "দেশি / উল্লেখ নেই" : "Native / not recorded"),
                    Sex: SexLabel(a.Sex, bn),
                    AgeMonths: ageMonths,
                    WeightKg: a.LatestWeightKg,
                    Status: StatusLabel(a.Status, bn));
            })
            .ToList();
    }

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

    private static string StatusLabel(AnimalStatus status, bool bn) => status switch
    {
        AnimalStatus.Active => bn ? "খামারে আছে" : "On farm",
        AnimalStatus.Quarantined => bn ? "আলাদা রাখা (কোয়ারেন্টাইন)" : "In quarantine",
        AnimalStatus.Sold => bn ? "বিক্রি হয়েছে" : "Sold",
        AnimalStatus.Slaughtered => bn ? "জবাই হয়েছে" : "Slaughtered",
        AnimalStatus.Dead => bn ? "মারা গেছে" : "Died",
        AnimalStatus.Transferred => bn ? "অন্যত্র পাঠানো" : "Transferred out",
        _ => status.ToString(),
    };
}
