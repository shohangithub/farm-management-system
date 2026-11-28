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
        var age = Age(animal.DateOfBirth, animal.ExitDate ?? context.Today, bn,
            s => context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(s) : s);
        return $"{(bn ? "ট্যাগ" : "Tag")} {AnimalLabels.Tag(animal, context)} · {Farm360.Application.Reporting.Definitions.Feeding.FeedReportText.Species(animal.Species).For(context.Language)} · {Farm360.Application.Reporting.Definitions.Feeding.FeedReportText.Sex(animal.Sex).For(context.Language)} · {(bn ? "বয়স" : "age")} {age} · "
            + $"{(bn ? "খামারে আছে" : "on farm since")} {AnimalLabels.Day(animal.AcquisitionDate, context.Language, context.BengaliNumerals)}";
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

        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;
        string Day(DateOnly d) => AnimalLabels.Day(d, context.Language, context.BengaliNumerals);
        string Num(string formatted) => context.BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(formatted) : formatted;
        string Tk(decimal v, string format) => "৳ " + Num(v.ToString(format, CultureInfo.InvariantCulture));
        string Kg(decimal v) => Num(v.ToString("N1", CultureInfo.InvariantCulture)) + T(" kg", " কেজি");
        string Whole(int v) => Num(v.ToString(CultureInfo.InvariantCulture));
        var assigned = T("Assigned", "নির্ধারিত");
        var unassigned = T("Unassigned", "নির্ধারিত নয়");

        var breedName = breed?.Name ?? T("Native / Cross", "দেশি / সংকর");
        var shedName = shed?.ShedNumber ?? (shedId.HasValue ? assigned : unassigned);
        var penName = pen?.PenNumber ?? (penId.HasValue ? assigned : unassigned);

        // Computations
        var today = animal.ExitDate ?? context.Today; // age stops at sale/death
        var ageDays = AnimalAge.Days(animal.DateOfBirth, today);
        var ageFormatted = $"{Age(animal.DateOfBirth, today, bn, Num)} ({Whole(ageDays)}{T(" days", " দিন")})";

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
        var weightTimesRate = T($"Latest weight {Kg(latestWeight)} × {Tk(marketPricePerKg, "N2")}/kg",
            $"সর্বশেষ ওজন {Kg(latestWeight)} × {Tk(marketPricePerKg, "N2")}/কেজি");
        var valueBasis = onHand
            ? (rateOverride is null && price.ValuedAtCost
                ? T("Valued at cost (farm setting): value = cost so far", "ক্রয়মূল্যে ধরা (খামারের নিয়ম): মূল্য = এ পর্যন্ত খরচ")
                : rateOverride is null && price.PerHeadBdt > 0m
                    ? weightTimesRate + T($" + {Tk(price.PerHeadBdt, "N0")} by-products per head", $" + প্রতি পশুতে উপজাত {Tk(price.PerHeadBdt, "N0")}")
                    : weightTimesRate)
            : animal.Status is AnimalStatus.Sold or AnimalStatus.Slaughtered
                ? T("Sale price received", "পাওয়া বিক্রয়মূল্য")
                : animal.Status == AnimalStatus.Dead
                    ? T("Animal died — no realisable value", "পশুটি মারা গেছে — কোনো মূল্য পাওয়া যাবে না")
                    : T("Left the farm — no realisable value", "খামার ছেড়ে গেছে — কোনো মূল্য পাওয়া যাবে না");

        var notRecorded = T("Not recorded", "লেখা হয়নি");
        var noWeighing = T("No weighing record", "ওজনের কোনো রেকর্ড নেই");
        string WeighedOn(DateOnly d) => T("Weighed on ", "ওজন নেওয়া হয়েছে ") + Day(d);

        var statusContext = animal.Status switch
        {
            AnimalStatus.Sold => T("Sold on ", "বিক্রি ") + (animal.SaleDate is { } sd ? Day(sd) : string.Empty)
                + (animal.BuyerName is { Length: > 0 } b ? T($" to {b}", $" — ক্রেতা {b}") : string.Empty)
                + T(" for ", "; দাম ") + Tk(animal.SalePriceBdt ?? 0m, "N0"),
            AnimalStatus.Dead => T("Died on ", "মৃত্যু ") + (animal.DeathDate is { } dd ? Day(dd) : string.Empty)
                + (animal.DisposalReason is { } reason ? $" ({DisposalLabel(reason).For(context.Language)})" : string.Empty),
            _ => string.IsNullOrWhiteSpace(animal.QuarantineReason)
                ? T("Normal herd condition", "পালের স্বাভাবিক অবস্থা")
                : T("Quarantine: ", "পৃথক রাখা: ") + animal.QuarantineReason,
        };

        var rows = new List<AnimalSummaryReportRow>
        {
            // ── Section 1: Demographics ───────────────────────────────────────
            new("1. Identification & Demographics", T("Tag", "ট্যাগ"), AnimalLabels.Tag(animal, context), animal.Tag.TagType switch
            {
                TagType.EarTag => T("Ear tag", "কানের ট্যাগ"),
                TagType.Rfid => T("RFID tag", "RFID ট্যাগ"),
                _ => T("Manual tag number", "হাতে লেখা ট্যাগ নম্বর"),
            }),
            new("1. Identification & Demographics", T("Species & breed", "প্রজাতি ও জাত"),
                $"{Farm360.Application.Reporting.Definitions.Feeding.FeedReportText.Species(animal.Species).For(context.Language)} · {breedName}",
                breed is null ? T("Breed not recorded", "জাত লেখা হয়নি") : T("Registered breed", "নিবন্ধিত জাত")),
            new("1. Identification & Demographics", T("Sex", "লিঙ্গ"),
                Farm360.Application.Reporting.Definitions.Feeding.FeedReportText.Sex(animal.Sex).For(context.Language), "—"),
            new("1. Identification & Demographics", T("Date of birth & age", "জন্মতারিখ ও বয়স"), $"{Day(animal.DateOfBirth)} ({ageFormatted})",
                animal.ExitDate is { } exit
                    ? T($"Age counted up to {Day(exit)}, when it left the farm", $"বয়স গোনা হয়েছে {Day(exit)} পর্যন্ত, যেদিন খামার ছেড়েছে")
                    : T("Age as of today", "আজ পর্যন্ত বয়স")),
            new("1. Identification & Demographics", T("How it came to the farm", "কীভাবে খামারে এসেছে"),
                T($"{AcquisitionLabel(animal.AcquisitionType).En} on {Day(animal.AcquisitionDate)}",
                  $"{AcquisitionLabel(animal.AcquisitionType).Bn} {Day(animal.AcquisitionDate)}"),
                T("Purchase cost: ", "ক্রয়মূল্য: ") + Tk(purchaseCost, "N0")),
            new("1. Identification & Demographics", T("Current status", "বর্তমান অবস্থা"),
                Farm360.Application.Reporting.Definitions.Feeding.FeedReportText.Status(animal.Status).For(context.Language),
                statusContext),

            // ── Section 2: Housing & Location ─────────────────────────────────
            new("2. Housing & Location", T("Shed", "শেড"), shedName,
                shedId.HasValue ? T("Current housing", "বর্তমান থাকার জায়গা") : T("No shed assigned", "কোনো শেড দেওয়া হয়নি")),
            new("2. Housing & Location", T("Pen / stall", "খোপ / স্টল"), penName,
                penId.HasValue ? T($"In shed {shedName}", $"শেড {shedName}-এ") : T("No pen assigned", "কোনো খোপ দেওয়া হয়নি")),
            new("2. Housing & Location", T("Batch", "ব্যাচ"),
                batch?.Name ?? (animal.BatchId.HasValue ? T("Batch not found", "ব্যাচ পাওয়া যায়নি") : T("Not in a batch", "কোনো ব্যাচে নেই")),
                T("Group the animal is managed with", "যে দলের সাথে পশুটি পালন করা হয়")),

            // ── Section 3: Growth & Performance ───────────────────────────────
            new("3. Growth & Weight KPIs", T("Latest weight", "সর্বশেষ ওজন"), animal.LatestWeightKg.HasValue ? Kg(latestWeight) : notRecorded,
                animal.LatestWeightDate.HasValue ? WeighedOn(animal.LatestWeightDate.Value) : noWeighing),
            new("3. Growth & Weight KPIs", T("First recorded weight", "প্রথম লেখা ওজন"), weightRecords.Count > 0 ? Kg(initialWeight) : notRecorded,
                weightRecords.Count > 0 ? WeighedOn(weightRecords[0].RecordedDate) : noWeighing),
            new("3. Growth & Weight KPIs", T("Weight gained since first weigh-in", "প্রথম ওজনের পর থেকে বেড়েছে"), Kg(totalGainKg),
                T($"Latest weight − first weight, over {weightRecords.Count} weigh-ins",
                  $"সর্বশেষ ওজন − প্রথম ওজন, {Whole(weightRecords.Count)} বার ওজন নেওয়া")),
            new("3. Growth & Weight KPIs", T("Average daily gain (kg/day)", "গড় দৈনিক বৃদ্ধি (কেজি/দিন)"),
                animal.AdgKgPerDay.HasValue
                    ? Num(adg.ToString("N3", CultureInfo.InvariantCulture)) + T(" kg/day", " কেজি/দিন")
                    : T("Not enough weigh-ins", "যথেষ্ট ওজন নেওয়া হয়নি"),
                T("First to latest weigh-in; see the Animal Weight & Growth report for gain in a chosen period",
                  "প্রথম থেকে সর্বশেষ ওজন পর্যন্ত; নির্দিষ্ট সময়ের বৃদ্ধির জন্য পশুর ওজন ও বৃদ্ধি রিপোর্ট দেখুন")),
            new("3. Growth & Weight KPIs", T("Body condition (1–5)", "শারীরিক অবস্থা (১–৫)"),
                animal.LatestBcs.HasValue ? Num(animal.LatestBcs.Value.ToString("N1", CultureInfo.InvariantCulture)) + " / " + Num("5") : notRecorded,
                T("Visual score of muscle and fat cover: 1 = very thin, 5 = very fat",
                  "মাংস ও চর্বির চোখে দেখা নম্বর: ১ = খুব রোগা, ৫ = খুব মোটা")),

            // ── Section 4: Cost & Financial Ledger ────────────────────────────
            new("4. Financial Investment Breakdown", T("Purchase cost", "ক্রয়মূল্য"), Tk(purchaseCost, "N2"),
                T("Price paid, or opening value if born on the farm", "দেওয়া দাম, খামারে জন্ম হলে শুরুর মূল্য")),
            new("4. Financial Investment Breakdown", T("Feed cost", "খাদ্য খরচ"), Tk(feedCost, "N2"),
                T($"{Kg(feedTotals.TotalKg)} confirmed feed over {Whole(feedTotals.Days)} days",
                  $"{Whole(feedTotals.Days)} দিনে নিশ্চিত খাদ্য {Kg(feedTotals.TotalKg)}")),
            new("4. Financial Investment Breakdown", T("Vet & medicine cost", "চিকিৎসা ও ওষুধ খরচ"), Tk(vetCost, "N2"),
                T("Vaccines, medicines and treatments", "টিকা, ওষুধ ও চিকিৎসা")),
            new("4. Financial Investment Breakdown", T("Labour cost (share)", "শ্রম খরচ (ভাগ)"), Tk(laborCost, "N2"),
                T("This animal's share of farm labour", "খামারের শ্রম খরচে এই পশুর ভাগ")),
            new("4. Financial Investment Breakdown", T("Overhead cost (share)", "পরোক্ষ খরচ (ভাগ)"), Tk(overheadCost, "N2"),
                T("This animal's share of utilities, shed upkeep and running costs", "বিদ্যুৎ-পানি, শেড রক্ষণাবেক্ষণ ও চলতি খরচে এই পশুর ভাগ")),
            new("4. Financial Investment Breakdown", T("Total cost so far", "এ পর্যন্ত মোট খরচ"), Tk(totalInvestment, "N2"),
                T("Sum of the five costs above", "ওপরের পাঁচটি খরচের যোগফল")),

            // ── Section 5: Valuation & Profit Projections ─────────────────────
            new("5. Valuation & Profit Projections", T("Live-weight rate (BDT/kg)", "জীবন্ত ওজনের দর (টাকা/কেজি)"),
                Tk(marketPricePerKg, "N2") + T(" / kg", " / কেজি"),
                rateOverride is null
                    ? (bn && !string.IsNullOrWhiteSpace(price.ExplanationBn) ? price.ExplanationBn : Capitalise(price.ExplanationEn))
                    : T("Rate entered for this report", "এই রিপোর্টের জন্য দেওয়া দর")),
            new("5. Valuation & Profit Projections", onHand ? T("Estimated value today", "আজকের আনুমানিক মূল্য") : T("Value realised", "পাওয়া মূল্য"),
                Tk(projectedValue, "N2"), valueBasis),
            new("5. Valuation & Profit Projections", onHand ? T("Expected profit / loss", "প্রত্যাশিত লাভ / ক্ষতি") : T("Actual profit / loss", "প্রকৃত লাভ / ক্ষতি"),
                Tk(projectedProfit, "N2"),
                projectedProfit >= 0
                    ? T("Profit: value is above total cost", "লাভ: মূল্য মোট খরচের চেয়ে বেশি")
                    : T("Loss: value is below total cost", "ক্ষতি: মূল্য মোট খরচের চেয়ে কম")),
            new("5. Valuation & Profit Projections", T("Return on cost (%)", "খরচের ওপর লাভ (%)"),
                Num(marginPct.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)) + "%",
                T("Profit ÷ total cost (", "লাভ ÷ মোট খরচ (") + Tk(totalInvestment, "N0") + ")"),
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

    /// <summary>"2y 3m" in English; "২ বছর ৩ মাস" in Bangla.</summary>
    private static string Age(DateOnly dateOfBirth, DateOnly asOf, bool bn, Func<string, string> num)
    {
        if (!bn)
        {
            return AnimalAge.Format(dateOfBirth, asOf);
        }

        string N(int v) => num(v.ToString(CultureInfo.InvariantCulture));
        var months = AnimalAge.CompletedMonths(dateOfBirth, asOf);
        if (months == 0)
        {
            return $"{N(AnimalAge.Days(dateOfBirth, asOf))} দিন";
        }

        if (months < 12)
        {
            return $"{N(months)} মাস";
        }

        var remainder = months % 12;
        return remainder > 0 ? $"{N(months / 12)} বছর {N(remainder)} মাস" : $"{N(months / 12)} বছর";
    }

    private static LocalizedText AcquisitionLabel(AcquisitionType type) => type switch
    {
        AcquisitionType.Purchased => new("Bought", "কেনা হয়েছে"),
        AcquisitionType.BornOnFarm => new("Born on farm", "খামারে জন্ম"),
        _ => new(type.ToString(), type.ToString()),
    };

    private static LocalizedText DisposalLabel(DisposalReason reason) => reason switch
    {
        DisposalReason.NaturalDeath => new("natural death", "স্বাভাবিক মৃত্যু"),
        DisposalReason.Disease => new("disease", "রোগ"),
        DisposalReason.Accident => new("accident", "দুর্ঘটনা"),
        DisposalReason.Slaughter => new("slaughter", "জবাই"),
        DisposalReason.Sale => new("sale", "বিক্রি"),
        _ => new("cause not recorded", "কারণ লেখা হয়নি"),
    };
}
