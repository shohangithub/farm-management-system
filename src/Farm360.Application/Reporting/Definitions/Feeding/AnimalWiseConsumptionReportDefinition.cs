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
/// Report F4 — Animal-wise Comprehensive Feed & Consumables Consumption Statement.
/// Shows per-animal feed intake, daily averages, ration costs, and medical treatment expenses.
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

    public override LocalizedText Title => new("Animal-wise Consumption & Feeding Cost Report", "পশুভিত্তিক খাদ্য গ্রহণ ও খরচ প্রতিবেদন");

    public override LocalizedText Description => new(
        "Comprehensive feed intake, days fed, average daily ration, and feeding expenditure per animal over the selected period.",
        "নির্দিষ্ট সময়কালে প্রতিটি পশুর মোট খাদ্য গ্রহণ, দৈনিক গড় মাত্রা ও খাদ্য খরচের পশুভিত্তিক তুলনামূলক প্রতিবেদন।");

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
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("tag", new LocalizedText("Tag ID", "ট্যাগ নং"), r => r.TagId, width: 22f, relative: false),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("name", new LocalizedText("Animal Name / Breed", "পশুর নাম / জাত"), r => r.AnimalName, width: 2.5f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("species", new LocalizedText("Species", "প্রজাতি"), r => r.Species, width: 1.4f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("location", new LocalizedText("Location / Batch", "স্থান / ব্যাচ"), r => r.LocationOrBatch, width: 1.8f),
        ReportColumn<AnimalWiseConsumptionReportRow>.WholeNumber("daysFed", new LocalizedText("Days Fed", "খাওয়ানোর দিন"), r => r.DaysFed, widthMm: 16f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Number("feedKg", new LocalizedText("Total Feed (kg)", "মোট খাদ্য (কেজি)"), r => r.TotalFeedKg, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Number("avgKg", new LocalizedText("Avg Feed/Day (kg)", "গড় দৈনিক খাদ্য"), r => r.DailyAvgFeedKg, decimals: 2, widthMm: 22f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("rate", new LocalizedText("Avg Rate (BDT)", "গড় দর (টাকা)"), r => r.AvgUnitCostBdt, decimals: 2, widthMm: 20f, aggregate: ReportAggregate.None),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("feedCost", new LocalizedText("Feed Cost (BDT)", "খাদ্য খরচ"), r => r.FeedCostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("medCost", new LocalizedText("Med/Treat (BDT)", "চিকিৎসা খরচ"), r => r.TreatmentCostBdt, decimals: 2, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("totalCost", new LocalizedText("Total Cost (BDT)", "মোট খরচ"), r => r.TotalConsumptionCostBdt, decimals: 2, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalWiseConsumptionReportRow>.Money("dailyCost", new LocalizedText("Cost/Day (BDT)", "দৈনিক খরচ"), r => r.DailyAvgCostBdt, decimals: 2, widthMm: 20f),
        ReportColumn<AnimalWiseConsumptionReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, width: 16f, relative: false),
    ];

    public override ReportGroup<AnimalWiseConsumptionReportRow>? Group =>
        ReportGroup<AnimalWiseConsumptionReportRow>.By(
            r => r.LocationOrBatch,
            r => $"Group / Location: {r.LocationOrBatch}");

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = context.Range("period");

        var animalId = context.IdOrNull("animalId");
        if (animalId.HasValue)
        {
            var animal = await _animals.GetByIdAsync(animalId.Value, cancellationToken).ConfigureAwait(false);
            if (animal is not null)
            {
                return $"Tag {animal.Tag.TagId} · {animal.Species} · Period {period.From:dd-MMM-yyyy} to {period.To:dd-MMM-yyyy}";
            }
        }

        return $"Herd Animal-wise Feed & Consumption Statement ({period.From:dd-MMM-yyyy} to {period.To:dd-MMM-yyyy})";
    }

    protected override async Task<IReadOnlyList<AnimalWiseConsumptionReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.Id("farmId");
        var period = context.Range("period");
        var animalId = context.IdOrNull("animalId");
        var batchId = context.IdOrNull("batchId");

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

        // Preload breeds
        var breedIds = animals.Select(a => a.BreedId).Distinct().ToList();
        var breedsById = new Dictionary<Guid, string>();
        foreach (var bId in breedIds)
        {
            var breed = await _breeds.GetByIdAsync(bId, cancellationToken).ConfigureAwait(false);
            if (breed is not null)
            {
                breedsById[bId] = breed.Name;
            }
        }

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

        var rows = new List<AnimalWiseConsumptionReportRow>(animalGroups.Count);

        foreach (var group in animalGroups)
        {
            var id = group.Key;
            animalsById.TryGetValue(id, out var animal);

            var tagId = animal?.Tag.TagId ?? id.ToString()[..8];
            var breedName = animal != null && breedsById.TryGetValue(animal.BreedId, out var bName) ? bName : "Standard";
            var animalName = $"{breedName} ({animal?.Sex.ToString() ?? "—"})";
            var species = animal?.Species.ToString() ?? "Livestock";

            var locationOrBatch = "Main Herd";
            if (animal?.BatchId.HasValue == true && batchesById.TryGetValue(animal.BatchId.Value, out var batchName))
            {
                locationOrBatch = $"Batch: {batchName}";
            }

            var daysFed = group.Select(a => a.EntryDate).Distinct().Count();
            var totalFeedKg = group.Sum(a => a.AllocatedKg);
            var feedCostBdt = group.Sum(a => a.AllocatedCostBdt);
            var dailyAvgFeedKg = daysFed > 0 ? decimal.Round(totalFeedKg / daysFed, 2, MidpointRounding.AwayFromZero) : 0m;
            var avgUnitCostBdt = totalFeedKg > 0 ? decimal.Round(feedCostBdt / totalFeedKg, 2, MidpointRounding.AwayFromZero) : 0m;

            // Fetch medical treatments in that period for this animal
            decimal treatmentCost = 0m;
            var treatments = await _treatments.GetByAnimalIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (treatments.Count > 0)
            {
                treatmentCost = treatments
                    .Where(t => t.StartDate >= period.From && t.StartDate <= period.To)
                    .Sum(t => t.CostBdt);
            }

            var totalConsumptionCost = feedCostBdt + treatmentCost;
            var dailyAvgCost = daysFed > 0 ? decimal.Round(totalConsumptionCost / daysFed, 2, MidpointRounding.AwayFromZero) : 0m;
            var status = animal?.Status.ToString() ?? "Active";

            rows.Add(new AnimalWiseConsumptionReportRow(
                TagId: tagId,
                AnimalName: animalName,
                Species: species,
                LocationOrBatch: locationOrBatch,
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

        return rows
            .OrderByDescending(r => r.TotalConsumptionCostBdt)
            .ThenBy(r => r.TagId)
            .ToList();
    }
}
