using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <param name="AnimalName">Breed and sex, e.g. "Sahiwal · Male".</param>
/// <param name="LocationOrBatch">The batch name, or empty when the animal is not in a batch.</param>
public sealed record AnimalWiseConsumptionReportRow(
    string TagId,
    string AnimalName,
    string Species,
    string LocationOrBatch,
    int DaysFed,
    decimal TotalFeedKg,
    decimal DailyAvgFeedKg,
    decimal AvgUnitCostBdt,
    decimal FeedCostBdt,
    decimal TreatmentCostBdt,
    decimal TotalConsumptionCostBdt,
    decimal DailyAvgCostBdt,
    string Status);

/// <summary>
/// Report F4 — Animal-wise feed and treatment cost: per animal, how much it ate over the period,
/// what the feed cost, what its treatments cost, and the cost per day it was fed.
/// </summary>
public sealed class AnimalWiseConsumptionReportDefinition : ReportDefinition<AnimalWiseConsumptionReportRow>
{
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IAnimalBatchRepository _batches;
    private readonly IMedicalTreatmentRepository _treatments;

    public AnimalWiseConsumptionReportDefinition(
        IAnimalFeedAllocationRepository allocations,
        IAnimalRepository animals,
        IBreedRepository breeds,
        IAnimalBatchRepository batches,
        IMedicalTreatmentRepository treatments)
    {
        _allocations = allocations;
        _animals = animals;
        _breeds = breeds;
        _batches = batches;
        _treatments = treatments;
    }

    public override string Key => "feeding.animal-consumption";

    public override LocalizedText Title => new("Feed & Treatment Cost by Animal", "পশুভিত্তিক খাদ্য ও চিকিৎসা খরচ");

    public override LocalizedText Description => new(
        "How much each animal ate in the period, what its feed and treatments cost, and its cost per day.",
        "নির্বাচিত সময়ে প্রতিটি পশু কত খাদ্য খেয়েছে, তার খাদ্য ও চিকিৎসায় কত খরচ হয়েছে এবং দৈনিক খরচ কত।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange(name: "period", @default: "current-month", required: true),
        ReportParameter.Batch(name: "batchId", required: false),
        ReportParameter.Animal(name: "animalId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<AnimalWiseConsumptionReportRow>> Columns =>
    [
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.TagId, width: 1.6f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("species", new LocalizedText("Species", "প্রজাতি"), r => r.Species, width: 1.2f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("name", new LocalizedText("Breed · sex", "জাত · লিঙ্গ"), r => r.AnimalName, width: 1.8f),
        ReportColumn<AnimalWiseConsumptionReportRow>.WholeNumber("daysFed", new LocalizedText("Days fed", "খাওয়ানোর দিন"), r => r.DaysFed, widthMm: 16f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Number("feedKg", new LocalizedText("Feed eaten (kg)", "খাওয়া খাদ্য (কেজি)"), r => r.TotalFeedKg, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Number("avgKg", new LocalizedText("Feed per day (kg/day)", "দৈনিক খাদ্য (কেজি/দিন)"), r => r.DailyAvgFeedKg, decimals: 2, widthMm: 20f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("rate", new LocalizedText("Feed rate (BDT/kg)", "খাদ্যের দর (টাকা/কেজি)"), r => r.AvgUnitCostBdt, decimals: 2, widthMm: 20f, aggregate: ReportAggregate.None),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("feedCost", new LocalizedText("Feed cost (BDT)", "খাদ্য খরচ (টাকা)"), r => r.FeedCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("medCost", new LocalizedText("Treatment cost (BDT)", "চিকিৎসা খরচ (টাকা)"), r => r.TreatmentCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("totalCost", new LocalizedText("Total cost (BDT)", "মোট খরচ (টাকা)"), r => r.TotalConsumptionCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("dailyCost", new LocalizedText("Cost per day (BDT/day)", "দৈনিক খরচ (টাকা/দিন)"), r => r.DailyAvgCostBdt, decimals: 2, widthMm: 22f),
    ];

    public override ReportGroup<AnimalWiseConsumptionReportRow>? Group =>
        ReportGroup<AnimalWiseConsumptionReportRow>.ByLocalized(
            r => r.LocationOrBatch,
            r => string.IsNullOrEmpty(r.LocationOrBatch)
                ? new LocalizedText("Not in a batch", "কোনো ব্যাচে নেই")
                : new LocalizedText($"Batch: {r.LocationOrBatch}", $"ব্যাচ: {r.LocationOrBatch}"));

    public override LocalizedText? GrandTotalLabel => new("Total for all animals shown", "দেখানো সব পশুর মোট");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one animal over the whole period; its feed includes its own ration and its share of any shed, batch or pen ration.",
            "প্রতিটি সারি পুরো সময়ের জন্য একটি পশু; তার খাদ্যে নিজস্ব রেশন এবং শেড, ব্যাচ বা পেনের রেশনের ভাগ দুটোই ধরা আছে।"),
        new("Days fed counts the days with any feed recorded; feed per day and cost per day are divided by days fed, not by the length of the period.",
            "খাওয়ানোর দিন মানে যেসব দিনে খাদ্য রেকর্ড আছে; দৈনিক খাদ্য ও দৈনিক খরচ খাওয়ানোর দিন দিয়ে ভাগ করা, পুরো সময়ের দিন দিয়ে নয়।"),
        new("Feed rate (BDT/kg) = feed cost ÷ feed eaten, the average price of the feed this animal ate.",
            "খাদ্যের দর (টাকা/কেজি) = খাদ্য খরচ ÷ খাওয়া খাদ্য, অর্থাৎ এই পশুর খাওয়া খাদ্যের গড় দাম।"),
        new("Treatment cost is the cost of treatments started in the period; total cost = feed cost + treatment cost.",
            "চিকিৎসা খরচ হলো এই সময়ে শুরু হওয়া চিকিৎসার খরচ; মোট খরচ = খাদ্য খরচ + চিকিৎসা খরচ।"),
        new("Sold or dead animals show the date they left beside the tag and count only the days they were on the farm.",
            "বিক্রি হওয়া বা মৃত পশুর ট্যাগের পাশে খামার ছাড়ার তারিখ থাকে, আর শুধু খামারে থাকা দিনগুলো গোনা হয়।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var language = context.Language;
        var parts = new List<string> { FeedReportText.Period(context.Range("period")) };

        var animalId = context.IdOrNull("animalId");
        if (animalId.HasValue)
        {
            var animal = await _animals.GetByIdAsync(animalId.Value, cancellationToken).ConfigureAwait(false);
            if (animal is not null)
            {
                parts.Add(new LocalizedText("Tag ", "ট্যাগ ").For(language) + AnimalLabels.Tag(animal, context));
                parts.Add(FeedReportText.Species(animal.Species).For(language));
            }
        }

        var batchId = context.IdOrNull("batchId");
        if (batchId.HasValue)
        {
            var batch = await _batches.GetByIdAsync(batchId.Value, cancellationToken).ConfigureAwait(false);
            if (batch is not null)
            {
                parts.Add(new LocalizedText("Batch: ", "ব্যাচ: ").For(language) + batch.Name);
            }
        }

        if (parts.Count == 1)
        {
            parts.Add(new LocalizedText("All animals fed in the period", "এই সময়ে খাওয়ানো সব পশু").For(language));
        }

        return string.Join(" · ", parts);
    }

    protected override async Task<IReadOnlyList<AnimalWiseConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var period = context.Range("period");
        var animalId = context.IdOrNull("animalId");
        var batchId = context.IdOrNull("batchId");
        var language = context.Language;

        var allocations = await _allocations
            .GetByFarmAsync(farmId, period.From, period.To, animalId, batchId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (allocations.Count == 0)
        {
            return [];
        }

        var animalGroups = allocations.GroupBy(a => a.AnimalId).ToList();
        var animalIds = animalGroups.Select(g => g.Key).Distinct().ToList();

        var animals = await _animals
            .GetByIdsAsync(animalIds, cancellationToken)
            .ConfigureAwait(false);

        var animalsById = animals.ToDictionary(a => a.Id);

        // Breeds are a small master list: one query, not one per animal.
        var breedsById = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(b => b.Id, b => b.Name);

        // Preload batches
        var batchIds = animals.Where(a => a.BatchId.HasValue).Select(a => a.BatchId!.Value).Distinct().ToList();
        var batchesById = new Dictionary<Guid, string>();
        foreach (var bId in batchIds)
        {
            var batch = await _batches.GetByIdAsync(bId, cancellationToken).ConfigureAwait(false);
            if (batch is not null)
            {
                batchesById[bId] = batch.Name;
            }
        }

        // Treatment cost for every animal in the period from the farm's treatments, fetched in
        // pages — previously one query per animal, which on a 400-head farm was 400 round trips.
        var treatmentCostByAnimal = await LoadTreatmentCostsAsync(farmId, animalId, period, cancellationToken).ConfigureAwait(false);

        var notRecorded = FeedReportText.NotRecorded.For(language);
        var rows = new List<AnimalWiseConsumptionReportRow>(animalGroups.Count);

        foreach (var group in animalGroups)
        {
            var id = group.Key;
            animalsById.TryGetValue(id, out var animal);

            string tagId, animalName, species, status, batchName;
            if (animal is null)
            {
                tagId = new LocalizedText("Unknown animal", "অজানা পশু").For(language);
                animalName = notRecorded;
                species = notRecorded;
                status = notRecorded;
                batchName = string.Empty;
            }
            else
            {
                tagId = AnimalLabels.Tag(animal, context);
                var breed = breedsById.TryGetValue(animal.BreedId, out var bName) && !string.IsNullOrWhiteSpace(bName)
                    ? bName
                    : new LocalizedText("Breed not recorded", "জাত রেকর্ড নেই").For(language);
                animalName = $"{breed} · {FeedReportText.Sex(animal.Sex).For(language)}";
                species = FeedReportText.Species(animal.Species).For(language);
                status = FeedReportText.Status(animal.Status).For(language);
                batchName = animal.BatchId is { } b && batchesById.TryGetValue(b, out var name) ? name : string.Empty;
            }

            var daysFed = group.Select(a => a.EntryDate).Distinct().Count();
            var totalFeedKg = group.Sum(a => a.AllocatedKg);
            var feedCostBdt = group.Sum(a => a.AllocatedCostBdt);
            var dailyAvgFeedKg = daysFed > 0 ? decimal.Round(totalFeedKg / daysFed, 2, MidpointRounding.AwayFromZero) : 0m;
            var avgUnitCostBdt = totalFeedKg > 0 ? decimal.Round(feedCostBdt / totalFeedKg, 2, MidpointRounding.AwayFromZero) : 0m;

            var treatmentCost = treatmentCostByAnimal.GetValueOrDefault(id);

            var totalConsumptionCost = feedCostBdt + treatmentCost;
            var dailyAvgCost = daysFed > 0 ? decimal.Round(totalConsumptionCost / daysFed, 2, MidpointRounding.AwayFromZero) : 0m;

            rows.Add(new AnimalWiseConsumptionReportRow(
                TagId: tagId,
                AnimalName: animalName,
                Species: species,
                LocationOrBatch: batchName,
                DaysFed: daysFed,
                TotalFeedKg: totalFeedKg,
                DailyAvgFeedKg: dailyAvgFeedKg,
                AvgUnitCostBdt: avgUnitCostBdt,
                FeedCostBdt: feedCostBdt,
                TreatmentCostBdt: treatmentCost,
                TotalConsumptionCostBdt: totalConsumptionCost,
                DailyAvgCostBdt: dailyAvgCost,
                Status: status));
        }

        // Named batches first (A–Z), animals outside any batch last; within a group, by tag.
        return rows
            .OrderBy(r => string.IsNullOrEmpty(r.LocationOrBatch))
            .ThenBy(r => r.LocationOrBatch, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(r => r.TagId, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<Dictionary<Guid, decimal>> LoadTreatmentCostsAsync(
        Guid farmId,
        Guid? animalId,
        DateRange period,
        CancellationToken cancellationToken)
    {
        const int pageSize = 2000;
        var costs = new Dictionary<Guid, decimal>();
        var page = 1;

        while (true)
        {
            var (items, total) = await _treatments
                .GetPagedAsync(page, pageSize, farmId: farmId, animalId: animalId, ct: cancellationToken)
                .ConfigureAwait(false);

            foreach (var t in items)
            {
                if (period.Contains(t.StartDate))
                {
                    costs[t.AnimalId] = costs.GetValueOrDefault(t.AnimalId) + t.CostBdt;
                }
            }

            if (items.Count == 0 || page * pageSize >= total)
            {
                return costs;
            }

            page++;
        }
    }
}
