using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Farms;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Enums;
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
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IShedRepository _sheds;
    private readonly IPenRepository _pens;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalValuationService _valuation;
    private readonly IAnimalBatchRepository _batches;
    private readonly Farm360.Application.Intelligence.Pricing.IAnimalPriceResolver _prices;

    public AnimalSummaryReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IShedRepository sheds,
        IPenRepository pens,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed,
        IAnimalValuationService valuation,
        IAnimalBatchRepository batches,
        Farm360.Application.Intelligence.Pricing.IAnimalPriceResolver prices)
    {
        _batches = batches;
        _prices = prices;
        _animals = animals;
        _breeds = breeds;
        _sheds = sheds;
        _pens = pens;
        _ledgers = ledgers;
        _feed = feed;
        _valuation = valuation;
    }

    public override string Key => "livestock.animal-summary";

    public override LocalizedText Title => new("Animal Profile & Profit/Loss", "পশুর পরিচিতি ও লাভ-ক্ষতি");

    public override LocalizedText Description => new(
        "Everything about one animal on one page: who it is, where it lives, how it has grown, what it has cost so far and whether it is making or losing money.",
        "এক পাতায় একটি পশুর সব তথ্য: পরিচিতি, কোথায় আছে, কতটা বেড়েছে, এ পর্যন্ত কত খরচ হয়েছে এবং লাভে না ক্ষতিতে আছে।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        LiveWeightRateParameter.Create(_valuation),
    ];

    public override IReadOnlyList<ReportColumn<AnimalSummaryReportRow>> Columns =>
    [
        ReportColumn<AnimalSummaryReportRow>.Text("metric", new LocalizedText("Item", "বিষয়"), r => r.Metric, width: 3.2f),
        ReportColumn<AnimalSummaryReportRow>.Text("value", new LocalizedText("Value", "মান"), r => r.Value, width: 2.8f),
        ReportColumn<AnimalSummaryReportRow>.Text("context", new LocalizedText("What it means / where it comes from", "অর্থ / কোথা থেকে এসেছে"), r => r.Context, width: 5.0f),
    ];

    public override bool ShowGrandTotals => false;

    public override ReportGroup<AnimalSummaryReportRow>? Group =>
        ReportGroup<AnimalSummaryReportRow>.ByLocalized(r => r.Section, r => SectionLabel(r.Section));

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one fact about the animal; figures are as of today, or as of the sale or death date if it has left the farm.",
            "প্রতিটি সারি পশুটির একটি তথ্য; হিসাব আজকের দিন পর্যন্ত, আর পশুটি খামার ছেড়ে গেলে বিক্রি বা মৃত্যুর দিন পর্যন্ত।"),
        new("Age stops at the sale or death date.",
            "বিক্রি বা মৃত্যুর তারিখে বয়স গণনা থেমে যায়।"),
        new("Total cost = purchase price + confirmed feed + vet + labour + overhead share booked to this animal.",
            "মোট খরচ = কেনার দাম + নিশ্চিত খাদ্য + চিকিৎসা + শ্রম + এই পশুর ভাগের অন্যান্য খরচ।"),
        new("For an animal on the farm, value = latest weight × live-weight rate; for a sold animal it is the sale price; a dead animal is worth nothing.",
            "খামারে থাকা পশুর মূল্য = সর্বশেষ ওজন × জীবন্ত ওজনের দর; বিক্রি হলে বিক্রয়মূল্য; মৃত পশুর মূল্য শূন্য।"),
        new("Profit/loss = value − total cost; return on cost (%) = profit ÷ total cost × 100.",
            "লাভ/ক্ষতি = মূল্য − মোট খরচ; খরচের উপর লাভ (%) = লাভ ÷ মোট খরচ × ১০০।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await _animals.GetByIdAsync(context.Id("animalId"), cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return null;
        }

        var bn = context.Language == ReportLanguage.Bn;
        var age = AnimalAge.Format(animal.DateOfBirth, animal.ExitDate ?? context.Today);
        return $"{(bn ? "ট্যাগ" : "Tag")} {AnimalLabels.Tag(animal)} · {SpeciesLabel(animal.Species)} · {animal.Sex} · {(bn ? "বয়স" : "age")} {age} · "
            + $"{(bn ? "খামারে আছে" : "on farm since")} {animal.AcquisitionDate.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";
    }

    protected override async Task<IReadOnlyList<AnimalSummaryReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");
        var rateOverride = LiveWeightRateParameter.ReadOverride(context);

        var animal = await _animals.GetByIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return [];
        }

        // The animal's own price — own sales of its breed, breed price, farm rule — as on the animal page.
        var price = await _prices.ForAnimalAsync(animal, context.Today, cancellationToken).ConfigureAwait(false);
        var marketPricePerKg = rateOverride ?? price.PricePerKgBdt;

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

        var batch = animal.BatchId is { } batchId
            ? await _batches.GetByIdAsync(batchId, cancellationToken).ConfigureAwait(false)
            : null;

        var breedName = breed?.Name ?? "Native / Cross";
        var shedName = shed?.ShedNumber ?? (shedId.HasValue ? "Assigned" : "Unassigned");
        var penName = pen?.PenNumber ?? (penId.HasValue ? "Assigned" : "Unassigned");

        // Computations
        var today = animal.ExitDate ?? context.Today; // age stops at sale/death
        var ageDays = AnimalAge.Days(animal.DateOfBirth, today);
        var ageFormatted = $"{AnimalAge.Format(animal.DateOfBirth, today)} ({ageDays} days)";

        var latestWeight = animal.LatestWeightKg ?? 0m;
        var adg = animal.AdgKgPerDay ?? 0m;

        var weightRecords = animal.WeightRecords.OrderBy(w => w.RecordedDate).ToList();
        var initialWeight = weightRecords.FirstOrDefault()?.Weight.WeightKg ?? latestWeight;
        var totalGainKg = Math.Max(0m, latestWeight - initialWeight);

        // Financials — the cost ledger (feed bucket mirrors confirmed feed allocations); without a
        // ledger, purchase price + allocated feed.
        var cost = _valuation.CostToDate(animal, ledger, feedTotals.TotalCostBdt);
        var purchaseCost = cost.PurchaseBdt;
        var feedCost = cost.FeedBdt;
        var vetCost = cost.VetBdt;
        var laborCost = cost.LabourBdt;
        var overheadCost = cost.OverheadBdt;
        var totalInvestment = cost.TotalBdt;

        // Valuation by status: on-farm animals at latest weight × rate, sold animals at what they
        // realised, dead animals at nothing.
        var onHand = _valuation.IsOnHand(animal);
        var projectedValue = _valuation.CurrentValue(animal, ledger, totalInvestment, price, rateOverride);
        var projectedProfit = projectedValue - totalInvestment;
        var marginPct = totalInvestment > 0 ? decimal.Round((projectedProfit / totalInvestment) * 100m, 1) : 0m;
        var valueBasis = onHand
            ? (rateOverride is null && price.ValuedAtCost
                ? "Valued at cost (farm setting): value = cost so far"
                : rateOverride is null && price.PerHeadBdt > 0m
                    ? $"Latest weight {latestWeight:N1} kg × ৳ {marketPricePerKg:N2}/kg + ৳ {price.PerHeadBdt:N0} by-products per head"
                    : $"Latest weight {latestWeight:N1} kg × ৳ {marketPricePerKg:N2}/kg")
            : animal.Status is Farm360.Domain.Livestock.Enums.AnimalStatus.Sold or Farm360.Domain.Livestock.Enums.AnimalStatus.Slaughtered
                ? "Sale price received"
                : animal.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Dead
                    ? "Animal died — no realisable value"
                    : "Left the farm — no realisable value";

        var rows = new List<AnimalSummaryReportRow>
        {
            // ── Section 1: Demographics ───────────────────────────────────────
            new("1. Identification & Demographics", "Tag", AnimalLabels.Tag(animal), animal.Tag.TagType switch { TagType.EarTag => "Ear tag", TagType.Rfid => "RFID tag", _ => "Manual tag number" }),
            new("1. Identification & Demographics", "Species & breed", $"{SpeciesLabel(animal.Species)} · {breedName}", breed is null ? "Breed not recorded" : "Registered breed"),
            new("1. Identification & Demographics", "Sex", animal.Sex.ToString(), "—"),
            new("1. Identification & Demographics", "Date of birth & age", $"{animal.DateOfBirth:dd-MMM-yyyy} ({ageFormatted})", animal.ExitDate is { } exit ? $"Age counted up to {exit:dd-MMM-yyyy}, when it left the farm" : "Age as of today"),
            new("1. Identification & Demographics", "How it came to the farm", $"{AcquisitionLabel(animal.AcquisitionType)} on {animal.AcquisitionDate:dd-MMM-yyyy}", $"Purchase cost: ৳ {purchaseCost:N0}"),
            new("1. Identification & Demographics", "Current status", StatusLabel(animal.Status),
                animal.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Sold
                    ? $"Sold on {animal.SaleDate:dd-MMM-yyyy}{(animal.BuyerName is { Length: > 0 } b ? $" to {b}" : "")} for ৳ {animal.SalePriceBdt:N0}"
                    : animal.Status == Farm360.Domain.Livestock.Enums.AnimalStatus.Dead
                        ? $"Died on {animal.DeathDate:dd-MMM-yyyy}{(animal.DisposalReason is { } reason ? $" ({DisposalLabel(reason)})" : "")}"
                        : string.IsNullOrWhiteSpace(animal.QuarantineReason) ? "Normal herd condition" : $"Quarantine: {animal.QuarantineReason}"),

            // ── Section 2: Housing & Location ─────────────────────────────────
            new("2. Housing & Location", "Shed", shedName, shedId.HasValue ? "Current housing" : "No shed assigned"),
            new("2. Housing & Location", "Pen / stall", penName, penId.HasValue ? $"In shed {shedName}" : "No pen assigned"),
            new("2. Housing & Location", "Batch", batch?.Name ?? (animal.BatchId.HasValue ? "Batch not found" : "Not in a batch"), "Group the animal is managed with"),

            // ── Section 3: Growth & Performance ───────────────────────────────
            new("3. Growth & Weight KPIs", "Latest weight", animal.LatestWeightKg.HasValue ? $"{latestWeight:N1} kg" : "Not recorded", animal.LatestWeightDate.HasValue ? $"Weighed on {animal.LatestWeightDate.Value:dd-MMM-yyyy}" : "No weighing record"),
            new("3. Growth & Weight KPIs", "First recorded weight", weightRecords.Count > 0 ? $"{initialWeight:N1} kg" : "Not recorded", weightRecords.Count > 0 ? $"Weighed on {weightRecords[0].RecordedDate:dd-MMM-yyyy}" : "No weighing record"),
            new("3. Growth & Weight KPIs", "Weight gained since first weigh-in", $"{totalGainKg:N1} kg", $"Latest weight − first weight, over {weightRecords.Count} weigh-ins"),
            new("3. Growth & Weight KPIs", "Average daily gain (kg/day)", animal.AdgKgPerDay.HasValue ? $"{adg:N3} kg/day" : "Not enough weigh-ins", "First to latest weigh-in; see the Animal Weight & Growth report for gain in a chosen period"),
            new("3. Growth & Weight KPIs", "Body condition (1–5)", animal.LatestBcs.HasValue ? $"{animal.LatestBcs.Value:N1} / 5" : "Not recorded", "Visual score of muscle and fat cover: 1 = very thin, 5 = very fat"),

            // ── Section 4: Cost & Financial Ledger ────────────────────────────
            new("4. Financial Investment Breakdown", "Purchase cost", $"৳ {purchaseCost:N2}", "Price paid, or opening value if born on the farm"),
            new("4. Financial Investment Breakdown", "Feed cost", $"৳ {feedCost:N2}", $"{feedTotals.TotalKg:N1} kg confirmed feed over {feedTotals.Days} days"),
            new("4. Financial Investment Breakdown", "Vet & medicine cost", $"৳ {vetCost:N2}", "Vaccines, medicines and treatments"),
            new("4. Financial Investment Breakdown", "Labour cost (share)", $"৳ {laborCost:N2}", "This animal's share of farm labour"),
            new("4. Financial Investment Breakdown", "Overhead cost (share)", $"৳ {overheadCost:N2}", "This animal's share of utilities, shed upkeep and running costs"),
            new("4. Financial Investment Breakdown", "Total cost so far", $"৳ {totalInvestment:N2}", "Sum of the five costs above"),

            // ── Section 5: Valuation & Profit Projections ─────────────────────
            new("5. Valuation & Profit Projections", "Live-weight rate (BDT/kg)", $"৳ {marketPricePerKg:N2} / kg", rateOverride is null ? Capitalise(price.ExplanationEn) : "Rate entered for this report"),
            new("5. Valuation & Profit Projections", onHand ? "Estimated value today" : "Value realised", $"৳ {projectedValue:N2}", valueBasis),
            new("5. Valuation & Profit Projections", onHand ? "Expected profit / loss" : "Actual profit / loss", $"৳ {projectedProfit:N2}", projectedProfit >= 0 ? "Profit: value is above total cost" : "Loss: value is below total cost"),
            new("5. Valuation & Profit Projections", "Return on cost (%)", $"{marginPct:+0.0;-0.0;0.0}%", $"Profit ÷ total cost (৳ {totalInvestment:N0})")
        };

        return rows;
    }

    private static string Capitalise(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private static LocalizedText SectionLabel(string section) => section switch
    {
        "1. Identification & Demographics" => new("1. Identity", "১. পরিচিতি"),
        "2. Housing & Location" => new("2. Where it is kept", "২. কোথায় রাখা হয়েছে"),
        "3. Growth & Weight KPIs" => new("3. Weight & growth", "৩. ওজন ও বৃদ্ধি"),
        "4. Financial Investment Breakdown" => new("4. Cost so far (BDT)", "৪. এ পর্যন্ত খরচ (টাকা)"),
        "5. Valuation & Profit Projections" => new("5. Value & profit/loss (BDT)", "৫. মূল্য ও লাভ-ক্ষতি (টাকা)"),
        _ => new(section, section),
    };

    private static string SpeciesLabel(AnimalSpecies species) => species switch
    {
        AnimalSpecies.CattleBeef => "Beef cattle",
        AnimalSpecies.CattleDairy => "Dairy cattle",
        _ => species.ToString(),
    };

    private static string AcquisitionLabel(AcquisitionType type) => type switch
    {
        AcquisitionType.Purchased => "Bought",
        AcquisitionType.BornOnFarm => "Born on farm",
        _ => type.ToString(),
    };

    private static string StatusLabel(AnimalStatus status) => status switch
    {
        AnimalStatus.Active => "On farm",
        AnimalStatus.Quarantined => "In quarantine",
        AnimalStatus.Sold => "Sold",
        AnimalStatus.Slaughtered => "Slaughtered",
        AnimalStatus.Dead => "Died",
        AnimalStatus.Transferred => "Transferred out",
        _ => status.ToString(),
    };

    private static string DisposalLabel(DisposalReason reason) => reason switch
    {
        DisposalReason.NaturalDeath => "natural death",
        DisposalReason.Disease => "disease",
        DisposalReason.Accident => "accident",
        DisposalReason.Slaughter => "slaughter",
        DisposalReason.Sale => "sale",
        _ => "cause not recorded",
    };
}
