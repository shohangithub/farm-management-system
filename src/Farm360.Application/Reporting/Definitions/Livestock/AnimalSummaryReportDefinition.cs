using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One attribute line in the single-animal comprehensive profile.</summary>
public sealed record AnimalSummaryReportRow(
    string Section,
    string Metric,
    string Value,
    string Context);

/// <summary>
/// Report A0 — Comprehensive single-animal summary dossier mirroring the Animal Details dashboard:
/// identity, location, weight and ADG trajectory, full cost accumulation, and live-weight profit/loss.
/// </summary>
public sealed class AnimalSummaryReportDefinition : ReportDefinition<AnimalSummaryReportRow>
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
    private readonly IPenRepository _pens;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;

    public AnimalSummaryReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IShedRepository sheds,
        IPenRepository pens,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed)
    {
        _animals = animals;
        _breeds = breeds;
        _sheds = sheds;
        _pens = pens;
        _ledgers = ledgers;
        _feed = feed;
    }

    public override string Key => "livestock.animal-summary";

    public override LocalizedText Title => new("Animal Comprehensive Summary Report", "পশুর সামগ্রিক সারসংক্ষেপ প্রতিবেদন");

    public override LocalizedText Description => new(
        "Complete animal profile sheet detailing demographics, location, weight & ADG trajectory, cost accumulation, and projected market profit/loss.",
        "পশুর পরিচিতি, অবস্থান, ওজন ও দৈনন্দিন বৃদ্ধি, ব্যয় খতিয়ান এবং বাজারমূল্যভিত্তিক সম্ভাব্য লাভ-ক্ষতি সংক্রান্ত সামগ্রিক প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        ReportParameter.Select(
            "pricePerKg",
            new LocalizedText("Live Weight Price Option", "জীবন্ত ওজনের দর নির্বাচন"),
            LiveWeightPriceOptions,
            @default: "400",
            required: false)
    ];

    public override IReadOnlyList<ReportColumn<AnimalSummaryReportRow>> Columns =>
    [
        ReportColumn<AnimalSummaryReportRow>.Text("metric", new LocalizedText("Metric / Indicator", "সূচক / বিবরণ"), r => r.Metric, width: 3.2f),
        ReportColumn<AnimalSummaryReportRow>.Text("value", new LocalizedText("Current Value", "বর্তমান মান"), r => r.Value, width: 2.8f),
        ReportColumn<AnimalSummaryReportRow>.Text("context", new LocalizedText("Notes & Analysis", "বিশ্লেষণ ও ভিত্তি"), r => r.Context, width: 5.0f),
    ];

    public override bool ShowGrandTotals => false;

    public override ReportGroup<AnimalSummaryReportRow>? Group =>
        ReportGroup<AnimalSummaryReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await _animals.GetByIdAsync(context.Id("animalId"), cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return null;
        }

        var ageMonths = Math.Max(0, (context.Today.DayNumber - animal.DateOfBirth.DayNumber) / 30);
        return $"Tag {animal.Tag.TagId} · {animal.Species} · {animal.Sex} · {ageMonths} mo · {animal.Status}";
    }

    protected override async Task<IReadOnlyList<AnimalSummaryReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var marketPricePerKg = context.Number("pricePerKg", 400m);
        if (marketPricePerKg <= 0)
        {
            marketPricePerKg = 400m;
        }

        var animal = await _animals.GetByIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return [];
        }

        var movement = animal.CurrentMovement;
        var shedId = movement?.ShedId;
        var penId = movement?.PenId;

        // Sequential resolve metadata to avoid EF Core DbContext multi-thread concurrency
        var breed = await _breeds.GetByIdAsync(animal.BreedId, cancellationToken).ConfigureAwait(false);
        var ledger = await _ledgers.GetByAnimalIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        var feedTotals = await _feed.GetTotalsForAnimalAsync(animalId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);

        Shed? shed = null;
        if (shedId.HasValue)
        {
            shed = await _sheds.GetByIdAsync(context.TenantId, shedId.Value, cancellationToken).ConfigureAwait(false);
        }

        Pen? pen = null;
        if (penId.HasValue)
        {
            pen = await _pens.GetByIdAsync(context.TenantId, penId.Value, cancellationToken).ConfigureAwait(false);
        }

        var breedName = breed?.Name ?? "Native / Cross";
        var shedName = shed?.ShedNumber ?? (shedId.HasValue ? "Assigned" : "Unassigned");
        var penName = pen?.PenNumber ?? (penId.HasValue ? "Assigned" : "Unassigned");

        // Computations
        var today = context.Today;
        var ageDays = Math.Max(0, today.DayNumber - animal.DateOfBirth.DayNumber);
        var ageMonths = ageDays / 30;
        var ageYears = ageMonths / 12;
        var remMonths = ageMonths % 12;
        var ageFormatted = ageYears > 0 ? $"{ageYears}y {remMonths}m ({ageDays} days)" : $"{ageMonths} months ({ageDays} days)";

        var latestWeight = animal.LatestWeightKg ?? 0m;
        var adg = animal.AdgKgPerDay ?? 0m;

        var weightRecords = animal.WeightRecords.OrderBy(w => w.RecordedDate).ToList();
        var initialWeight = weightRecords.FirstOrDefault()?.Weight.WeightKg ?? latestWeight;
        var totalGainKg = Math.Max(0m, latestWeight - initialWeight);

        // Financials
        var purchaseCost = animal.AcquisitionPriceBdt ?? ledger?.AcquisitionCostBdt ?? 0m;
        var feedCost = ledger?.TotalFeedCostBdt ?? feedTotals.TotalCostBdt;
        var vetCost = ledger?.TotalVetCostBdt ?? 0m;
        var laborCost = ledger?.TotalLaborCostBdt ?? 0m;
        var overheadCost = ledger?.TotalOverheadBdt ?? 0m;
        var totalInvestment = purchaseCost + feedCost + vetCost + laborCost + overheadCost;

        // Valuations
        var projectedValue = latestWeight * marketPricePerKg;
        var projectedProfit = projectedValue - totalInvestment;
        var marginPct = totalInvestment > 0 ? decimal.Round((projectedProfit / totalInvestment) * 100m, 1) : 0m;

        var rows = new List<AnimalSummaryReportRow>
        {
            // ── Section 1: Demographics ───────────────────────────────────────
            new("1. Identification & Demographics", "Tag Identification", animal.Tag.TagId, $"Type: {animal.Tag.TagType}"),
            new("1. Identification & Demographics", "Species & Breed", $"{animal.Species} · {breedName}", $"Breed ID: {animal.BreedId.ToString()[..8]}"),
            new("1. Identification & Demographics", "Sex & Gender", animal.Sex.ToString(), "Biological sex classification"),
            new("1. Identification & Demographics", "Date of Birth & Age", $"{animal.DateOfBirth:dd-MMM-yyyy} ({ageFormatted})", $"Born: {animal.DateOfBirth:yyyy-MM-dd}"),
            new("1. Identification & Demographics", "Acquisition Info", $"{animal.AcquisitionType} on {animal.AcquisitionDate:dd-MMM-yyyy}", $"Initial Cost: ৳ {purchaseCost:N0}"),
            new("1. Identification & Demographics", "Current Status", animal.Status.ToString().ToUpperInvariant(), string.IsNullOrWhiteSpace(animal.QuarantineReason) ? "Normal herd condition" : $"Quarantine: {animal.QuarantineReason}"),

            // ── Section 2: Housing & Location ─────────────────────────────────
            new("2. Housing & Location", "Shed Allocation", shedName, shedId.HasValue ? $"Shed ID: {shedId.Value.ToString()[..8]}" : "No active shed assigned"),
            new("2. Housing & Location", "Pen / Stall", penName, penId.HasValue ? $"Pen ID: {penId.Value.ToString()[..8]}" : "No active pen assigned"),
            new("2. Housing & Location", "Batch Assignment", animal.BatchId.HasValue ? animal.BatchId.Value.ToString()[..8] : "Individual (No Batch)", "Herd management grouping"),

            // ── Section 3: Growth & Performance ───────────────────────────────
            new("3. Growth & Weight KPIs", "Current / Latest Weight", $"{latestWeight:N1} kg", animal.LatestWeightDate.HasValue ? $"Recorded on {animal.LatestWeightDate.Value:dd-MMM-yyyy}" : "No weighing record"),
            new("3. Growth & Weight KPIs", "Initial Weight", $"{initialWeight:N1} kg", weightRecords.Count > 0 ? $"First recorded {weightRecords[0].RecordedDate:dd-MMM-yyyy}" : "Baseline"),
            new("3. Growth & Weight KPIs", "Lifetime Net Gain", $"{totalGainKg:N1} kg", $"Across {weightRecords.Count} weigh-in sessions"),
            new("3. Growth & Weight KPIs", "Average Daily Gain (ADG)", $"{adg:N3} kg/day", "Growth rate efficiency"),
            new("3. Growth & Weight KPIs", "Body Condition Score (BCS)", animal.LatestBcs.HasValue ? $"{animal.LatestBcs.Value:N1} / 5.0" : "Not evaluated", "Muscling & fat coverage assessment"),

            // ── Section 4: Cost & Financial Ledger ────────────────────────────
            new("4. Financial Investment Breakdown", "Acquisition / Purchase Cost", $"৳ {purchaseCost:N2}", "Original purchase or capital valuation"),
            new("4. Financial Investment Breakdown", "Feed & Nutrition Cost", $"৳ {feedCost:N2}", $"{feedTotals.TotalKg:N1} kg feed allocated across {feedTotals.Days} days"),
            new("4. Financial Investment Breakdown", "Veterinary & Healthcare Cost", $"৳ {vetCost:N2}", "Vaccinations, medications and clinic treatments"),
            new("4. Financial Investment Breakdown", "Labor Allocation", $"৳ {laborCost:N2}", "Apportioned barn management & labor"),
            new("4. Financial Investment Breakdown", "Overhead & Facilities", $"৳ {overheadCost:N2}", "Utilities, barn depreciation & operating share"),
            new("4. Financial Investment Breakdown", "Total Accumulated Investment", $"৳ {totalInvestment:N2}", "Full-absorption cumulative cost to date"),

            // ── Section 5: Valuation & Profit Projections ─────────────────────
            new("5. Valuation & Profit Projections", "Live Weight Market Rate", $"৳ {marketPricePerKg:N2} / kg", "Selected benchmark pricing basis"),
            new("5. Valuation & Profit Projections", "Estimated Market Value", $"৳ {projectedValue:N2}", $"Based on live weight: {latestWeight:N1} kg × ৳ {marketPricePerKg:N0}"),
            new("5. Valuation & Profit Projections", "Projected Net Profit / Margin", $"৳ {projectedProfit:N2}", projectedProfit >= 0 ? "PROFITABLE INVESTMENT" : "DEFICIT / LOSS"),
            new("5. Valuation & Profit Projections", "Return on Investment (ROI)", $"{marginPct:+0.0;-0.0;0.0}%", $"Net margin over total investment (৳ {totalInvestment:N0})")
        };

        return rows;
    }
}
