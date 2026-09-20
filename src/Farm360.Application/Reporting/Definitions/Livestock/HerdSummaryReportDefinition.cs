using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Farms.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One animal in the multi-animal herd summary register.</summary>
public sealed record HerdSummaryReportRow(
    string TagId,
    string Breed,
    string Sex,
    string Age,
    string Location,
    decimal LatestWeightKg,
    decimal AdgKg,
    decimal PurchaseCostBdt,
    decimal FeedCostBdt,
    decimal VetCostBdt,
    decimal TotalCostBdt,
    decimal ProjectedValueBdt,
    decimal ProjectedProfitBdt,
    string Status);

/// <summary>
/// Report H0 — Multi-animal master summary register showing every animal's demographics, current weight,
/// ADG, purchase cost, feed cost, healthcare cost, total investment, market valuation, and net profit/loss.
/// </summary>
public sealed class HerdSummaryReportDefinition : ReportDefinition<HerdSummaryReportRow>
{
    private static readonly IReadOnlyList<ReportSelectOption> LiveWeightPriceOptions =
    [
        new("350", new LocalizedText("৳ 350 / kg (Conservative / সাধারণ)", "৳ ৩৫০ / কেজি (সাধারণ)")),
        new("380", new LocalizedText("৳ 380 / kg (Moderate / মাঝারি)", "৳ ৩৮০ / কেজি (মাঝারি)")),
        new("400", new LocalizedText("৳ 400 / kg (Standard Benchmark / প্রমিত দর)", "৳ ৪০০ / কেজি (প্রমিত দর)")),
        new("420", new LocalizedText("৳ 420 / kg (High Quality Beef / উন্নত জাত)", "৳ ৪২০ / কেজি (উন্নত জাত)")),
        new("450", new LocalizedText("৳ 450 / kg (Eid / Festival Market / ঈদ বাজার)", "৳ ৪৫০ / কেজি (ঈদ বাজার)")),
        new("500", new LocalizedText("৳ 500 / kg (Peak Premium / সর্বোচ্চ প্রিমিয়াম)", "৳ ৫০০ / কেজি (সর্বোচ্চ প্রিমিয়াম)"))
    ];

    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IShedRepository _sheds;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;

    public HerdSummaryReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IShedRepository sheds,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed)
    {
        _animals = animals;
        _breeds = breeds;
        _sheds = sheds;
        _ledgers = ledgers;
        _feed = feed;
    }

    public override string Key => "livestock.herd-summary";

    public override LocalizedText Title => new("Herd Master Summary Register", "পশুর সামগ্রিক বিবরণ ও লাভ-ক্ষতি রেজিস্টার");

    public override LocalizedText Description => new(
        "Comprehensive herd-wide summary table displaying demographics, current weights, ADG, full cost breakdown, projected market valuation, and net profit/loss for every animal.",
        "খামারের সকল পশুর পরিচিতি, ওজন, দৈনিক বৃদ্ধি, মোট খরচ (ক্রয়, খাদ্য ও চিকিৎসা), বাজারমূল্য ও সম্ভাব্য লাভ-ক্ষতির পূর্ণাঙ্গ রেজিস্টার প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch(required: false),
        ReportParameter.Select(
            "pricePerKg",
            new LocalizedText("Live Weight Price Option", "জীবন্ত ওজনের দর নির্বাচন"),
            LiveWeightPriceOptions,
            @default: "400",
            required: false)
    ];

    public override IReadOnlyList<ReportColumn<HerdSummaryReportRow>> Columns =>
    [
        ReportColumn<HerdSummaryReportRow>.Text("tag", new LocalizedText("Tag ID", "ট্যাগ নম্বর"), r => r.TagId, 22f, false),
        ReportColumn<HerdSummaryReportRow>.Text("breed", new LocalizedText("Breed", "জাত"), r => r.Breed, 2.2f),
        ReportColumn<HerdSummaryReportRow>.Text("sex", new LocalizedText("Sex", "লিঙ্গ"), r => r.Sex, 14f, false),
        ReportColumn<HerdSummaryReportRow>.Text("age", new LocalizedText("Age", "বয়স"), r => r.Age, 16f, false),
        ReportColumn<HerdSummaryReportRow>.Text("location", new LocalizedText("Location", "অবস্থান"), r => r.Location, 2.0f),
        ReportColumn<HerdSummaryReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.LatestWeightKg, 1, 20f),
        ReportColumn<HerdSummaryReportRow>.Number("adg", new LocalizedText("ADG (kg/d)", "দৈনিক বৃদ্ধি"), r => r.AdgKg, 3, 20f),
        ReportColumn<HerdSummaryReportRow>.Money("purchaseCost", new LocalizedText("Purchase (৳)", "ক্রয় মূল্য"), r => r.PurchaseCostBdt, 0, 22f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("feedCost", new LocalizedText("Feed (৳)", "খাদ্য খরচ"), r => r.FeedCostBdt, 0, 22f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("vetCost", new LocalizedText("Vet / Care (৳)", "চিকিৎসা"), r => r.VetCostBdt, 0, 20f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("totalCost", new LocalizedText("Total Cost (৳)", "মোট খরচ"), r => r.TotalCostBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("projectedValue", new LocalizedText("Est. Value (৳)", "বাজারমূল্য"), r => r.ProjectedValueBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("projectedProfit", new LocalizedText("Net P/L (৳)", "সম্ভাব্য লাভ/ক্ষতি"), r => r.ProjectedProfitBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Text("status", new LocalizedText("Status", "অবস্থা"), r => r.Status, 18f, false),
    ];

    public override ReportGroup<HerdSummaryReportRow>? Group =>
        ReportGroup<HerdSummaryReportRow>.By(r => r.Status, r => $"Status: {r.Status}");

    public override bool ShowGrandTotals => true;

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return "Complete Herd Inventory, Cost & Valuation Register";
    }

    protected override async Task<IReadOnlyList<HerdSummaryReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = await ResolveFarmIdAsync(context, cancellationToken).ConfigureAwait(false);
        var batchId = context.IdOrNull("batchId");
        var marketPricePerKg = context.Number("pricePerKg", 400m);
        if (marketPricePerKg <= 0)
        {
            marketPricePerKg = 400m;
        }

        var (animals, _) = await _animals
            .GetPagedAsync(
                pageNumber: 1,
                pageSize: 5000,
                farmId: farmId == Guid.Empty ? null : farmId,
                batchId: batchId,
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (animals.Count == 0)
        {
            return [];
        }

        // Sequential pre-load farm ledgers, feed totals, sheds, and breeds to avoid DbContext concurrency
        var ledgers = await _ledgers.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false);
        var feedTotals = await _feed.GetTotalsForFarmAsync(farmId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);
        var breeds = await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var sheds = await _sheds.GetAllByFarmAsync(context.TenantId, farmId, cancellationToken).ConfigureAwait(false);

        var ledgersMap = ledgers.ToDictionary(l => l.AnimalId);
        var feedMap = feedTotals.ToDictionary(f => f.AnimalId);
        var breedsMap = breeds.ToDictionary(b => b.Id);
        var shedsMap = sheds.ToDictionary(s => s.Id);

        var today = context.Today;
        var rows = new List<HerdSummaryReportRow>(animals.Count);

        foreach (var a in animals)
        {
            ledgersMap.TryGetValue(a.Id, out var ledger);
            feedMap.TryGetValue(a.Id, out var feed);
            breedsMap.TryGetValue(a.BreedId, out var breed);

            var movement = a.CurrentMovement;
            string shedLabel = "—";
            if (movement?.ShedId.HasValue == true)
            {
                shedLabel = shedsMap.TryGetValue(movement.ShedId.Value, out var s)
                    ? s.ShedNumber
                    : "Shed";
            }

            var location = movement?.PenId.HasValue == true ? $"{shedLabel} / Pen" : shedLabel;

            var ageDays = Math.Max(0, today.DayNumber - a.DateOfBirth.DayNumber);
            var ageMonths = ageDays / 30;
            var ageYears = ageMonths / 12;
            var ageRem = ageMonths % 12;
            var ageLabel = ageYears > 0 ? $"{ageYears}y {ageRem}m" : $"{ageMonths}mo";

            var weight = a.LatestWeightKg ?? 0m;
            var adg = a.AdgKgPerDay ?? 0m;

            var purchaseCost = a.AcquisitionPriceBdt ?? ledger?.AcquisitionCostBdt ?? 0m;
            var feedCost = ledger?.TotalFeedCostBdt ?? feed.TotalCostBdt;
            var vetCost = ledger?.TotalVetCostBdt ?? 0m;
            var totalCost = ledger?.TotalCostBdt ?? (purchaseCost + feedCost + vetCost);

            var projectedValue = weight * marketPricePerKg;
            var projectedProfit = projectedValue - totalCost;

            rows.Add(new HerdSummaryReportRow(
                TagId: a.Tag.TagId,
                Breed: breed?.Name ?? $"{a.Species}",
                Sex: a.Sex.ToString(),
                Age: ageLabel,
                Location: location,
                LatestWeightKg: weight,
                AdgKg: adg,
                PurchaseCostBdt: purchaseCost,
                FeedCostBdt: feedCost,
                VetCostBdt: vetCost,
                TotalCostBdt: totalCost,
                ProjectedValueBdt: projectedValue,
                ProjectedProfitBdt: projectedProfit,
                Status: a.Status.ToString()));
        }

        return rows
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
