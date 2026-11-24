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
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>One animal in the multi-animal herd summary register.</summary>
public sealed record HerdSummaryReportRow(
    string TagId,
    string Breed,
    string Sex,
    string Age,
    string Location,
    decimal? LatestWeightKg,
    decimal? AdgKg,
    decimal PurchaseCostBdt,
    decimal FeedCostBdt,
    decimal VetCostBdt,
    decimal LabourOverheadBdt,
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
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IShedRepository _sheds;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalValuationService _valuation;

    public HerdSummaryReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IShedRepository sheds,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed,
        IAnimalValuationService valuation)
    {
        _animals = animals;
        _breeds = breeds;
        _sheds = sheds;
        _ledgers = ledgers;
        _feed = feed;
        _valuation = valuation;
    }

    public override string Key => "livestock.herd-summary";

    public override LocalizedText Title => new("Herd Cost, Value & Profit", "পালের খরচ, মূল্য ও লাভ-ক্ষতি");

    public override LocalizedText Description => new(
        "Is each animal making or losing money? Every animal with its weight, what it has cost so far (purchase, feed, vet and other costs), what it is worth and the profit or loss.",
        "প্রতিটি পশু লাভে না ক্ষতিতে আছে? প্রতিটি পশুর ওজন, এ পর্যন্ত খরচ (কেনা, খাদ্য, চিকিৎসা ও অন্যান্য), বর্তমান মূল্য এবং লাভ বা ক্ষতি।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4Landscape;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch(required: false),
        LiveWeightRateParameter.Create(_valuation),
    ];

    public override IReadOnlyList<ReportColumn<HerdSummaryReportRow>> Columns =>
    [
        ReportColumn<HerdSummaryReportRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.TagId, 2.6f),
        ReportColumn<HerdSummaryReportRow>.Text("breed", new LocalizedText("Breed / sex", "জাত / লিঙ্গ"), r => $"{r.Breed} · {r.Sex}", 2.2f),
        ReportColumn<HerdSummaryReportRow>.Text("age", new LocalizedText("Age", "বয়স"), r => r.Age, 16f, false),
        ReportColumn<HerdSummaryReportRow>.Text("location", new LocalizedText("Shed", "শেড"), r => r.Location, 1.4f),
        ReportColumn<HerdSummaryReportRow>.Number("weight", new LocalizedText("Latest weight (kg)", "সর্বশেষ ওজন (কেজি)"), r => r.LatestWeightKg, 1, 22f),
        // Lifetime daily gain (oldest-to-newest weight record, not bounded to any reporting
        // period) -- the Animal Weight & Growth report shows gain for a chosen period, and the two
        // legitimately differ.
        ReportColumn<HerdSummaryReportRow>.Number("adg", new LocalizedText("Daily gain (kg/day)", "দৈনিক বৃদ্ধি (কেজি/দিন)"), r => r.AdgKg, 3, 22f),
        ReportColumn<HerdSummaryReportRow>.Money("purchaseCost", new LocalizedText("Purchase (BDT)", "কেনার দাম (টাকা)"), r => r.PurchaseCostBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("feedCost", new LocalizedText("Feed (BDT)", "খাদ্য (টাকা)"), r => r.FeedCostBdt, 0, 22f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("otherCost", new LocalizedText("Vet, labour & overhead (BDT)", "চিকিৎসা, শ্রম ও অন্যান্য (টাকা)"), r => r.VetCostBdt + r.LabourOverheadBdt, 0, 26f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("totalCost", new LocalizedText("Total cost (BDT)", "মোট খরচ (টাকা)"), r => r.TotalCostBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("projectedValue", new LocalizedText("Value (BDT)", "মূল্য (টাকা)"), r => r.ProjectedValueBdt, 0, 24f, ReportAggregate.Sum),
        ReportColumn<HerdSummaryReportRow>.Money("projectedProfit", new LocalizedText("Profit / loss (BDT)", "লাভ / ক্ষতি (টাকা)"), r => r.ProjectedProfitBdt, 0, 24f, ReportAggregate.Sum),
    ];

    public override ReportGroup<HerdSummaryReportRow>? Group =>
        ReportGroup<HerdSummaryReportRow>.ByLocalized(r => r.Status, r => new LocalizedText($"Status: {r.Status}", $"অবস্থা: {r.Status}"));

    public override bool ShowGrandTotals => true;

    public override LocalizedText? GrandTotalLabel => new("Whole herd", "সমগ্র পাল");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one animal, grouped by status: animals on the farm first, then those that were sold, slaughtered, died or left.",
            "প্রতিটি সারি একটি পশু, অবস্থা অনুযায়ী ভাগ করা: খামারে থাকা পশু আগে, তারপর বিক্রি, জবাই, মৃত বা চলে যাওয়া পশু।"),
        new("Total cost = purchase + confirmed feed + vet + this animal's share of labour and overhead, from arrival up to today (or the sale/death date).",
            "মোট খরচ = কেনার দাম + নিশ্চিত খাদ্য + চিকিৎসা + শ্রম ও অন্যান্য খরচে এই পশুর ভাগ, আসার দিন থেকে আজ (বা বিক্রি/মৃত্যুর দিন) পর্যন্ত।"),
        new("Value: for an animal on the farm, latest weight × live-weight rate (an estimate); for a sold animal, the sale price; for a dead animal, zero.",
            "মূল্য: খামারে থাকা পশুর জন্য সর্বশেষ ওজন × জীবন্ত ওজনের দর (আনুমানিক); বিক্রি হলে বিক্রয়মূল্য; মৃত পশুর জন্য শূন্য।"),
        new("Profit / loss = value − total cost; for animals still on the farm it is expected, not yet earned.",
            "লাভ / ক্ষতি = মূল্য − মোট খরচ; খামারে থাকা পশুর ক্ষেত্রে এটি সম্ভাব্য, এখনো অর্জিত নয়।"),
        new("Age stops at the sale or death date.",
            "বিক্রি বা মৃত্যুর তারিখে বয়স গণনা থেমে যায়।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var asOf = context.Today.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var rate = LiveWeightRateParameter.EffectiveRate(context, _valuation).ToString("N0", CultureInfo.InvariantCulture);
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"{asOf} পর্যন্ত · খামারে থাকা পশুর মূল্য ৳ {rate}/কেজি জীবন্ত ওজন দরে"
            : $"As of {asOf} · animals on the farm valued at ৳ {rate} per live kg");
    }

    protected override async Task<IReadOnlyList<HerdSummaryReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var batchId = context.IdOrNull("batchId");
        var rateOverride = LiveWeightRateParameter.ReadOverride(context);

        // Fetches every page, not just the first 5000: a farm with more animals than that used to
        // have its grand totals (purchase/feed/vet/total cost, projected value) silently computed
        // over only the first 5000 while LOOKING complete -- the real guard against an oversized
        // report is ReportExecutionService's 200k-row cap, which throws instead of truncating
        // quietly, so this just needs to hand it the true, complete dataset.
        const int pageSize = 5000;
        var animals = new List<Farm360.Domain.Livestock.Animal>();
        var pageNumber = 1;
        while (true)
        {
            var (page, totalCount) = await _animals
                .GetPagedAsync(
                    pageNumber: pageNumber,
                    pageSize: pageSize,
                    farmId: farmId,
                    batchId: batchId,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // The count arrives with the first page: refuse an oversized register before loading the rest.
            context.EnsureWithinRowLimit(totalCount, Key);

            animals.AddRange(page);
            if (animals.Count >= totalCount || page.Count == 0)
            {
                break;
            }

            pageNumber++;
        }

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
        var bn = context.Language == ReportLanguage.Bn;
        var rows = new List<(int Order, HerdSummaryReportRow Row)>(animals.Count);

        foreach (var a in animals)
        {
            ledgersMap.TryGetValue(a.Id, out var ledger);
            feedMap.TryGetValue(a.Id, out var feed);
            breedsMap.TryGetValue(a.BreedId, out var breed);

            // Where the animal is kept now; an animal that has left the farm is kept nowhere.
            var movement = a.CurrentMovement;
            var location = "—";
            if (!a.HasLeftFarm && movement?.ShedId is { } shedId)
            {
                location = shedsMap.TryGetValue(shedId, out var s) ? s.ShedNumber : "—";
            }

            var ageLabel = AnimalAge.Format(a.DateOfBirth, a.ExitDate ?? today);


            // Cost ledger buckets (purchase falls back to the animal record); without a ledger,
            // purchase + allocated feed. The columns always sum to the total.
            var cost = _valuation.CostToDate(a, ledger, feed.TotalCostBdt);
            var totalCost = cost.TotalBdt;

            // On-farm animals at weight × rate, sold at realised revenue, dead at zero.
            var projectedValue = _valuation.CurrentValue(a, ledger, totalCost, rateOverride);
            var projectedProfit = projectedValue - totalCost;

            rows.Add(((int)a.Status, new HerdSummaryReportRow(
                TagId: AnimalLabels.Tag(a),
                Breed: breed?.Name ?? SpeciesLabel(a.Species, bn),
                Sex: SexLabel(a.Sex, bn),
                Age: ageLabel,
                Location: location,
                LatestWeightKg: a.LatestWeightKg,
                AdgKg: a.AdgKgPerDay,
                PurchaseCostBdt: cost.PurchaseBdt,
                FeedCostBdt: cost.FeedBdt,
                VetCostBdt: cost.VetBdt,
                LabourOverheadBdt: cost.LabourBdt + cost.OverheadBdt,
                TotalCostBdt: totalCost,
                ProjectedValueBdt: projectedValue,
                ProjectedProfitBdt: projectedProfit,
                Status: StatusLabel(a.Status, bn))));
        }

        // On-farm animals first, then the ones that left, in status order; bands follow it.
        return rows
            .OrderBy(r => r.Order)
            .ThenBy(r => r.Row.TagId, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.Row)
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
