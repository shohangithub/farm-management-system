using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Finance.Services;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One thing that happened to the animal, with what it cost or brought in.</summary>
public sealed record AnimalLifeHistoryReportRow(
    string SectionKey,
    string SectionLabel,
    DateOnly? Date,
    string Event,
    string Details,
    decimal? CostBdt,
    decimal? IncomeBdt);

/// <summary>
/// Report FN14 — one animal's whole life on the farm, from arrival to sale or death: how it came,
/// every weighing, feed month by month, every treatment and vaccination, its share of labour and
/// overhead, where it was kept, and the sale with what the buyer paid. The summary box adds up the
/// cost by kind, sets it against the sale price (or today's estimated value) and explains the result.
/// </summary>
/// <remarks>
/// The dated lines come from the same sources as the daily cost reports. The cost book
/// (<see cref="IAnimalValuationService.CostToDate"/>) is what every other report uses, so where the
/// dated lines do not add up to it — costs booked before dated records were kept, or edited
/// totals — the difference is shown as an "adjustment to match the cost book" line, and the report's
/// total cost always equals the Animal Cost to Date and Sales Register figures.
/// </remarks>
public sealed class AnimalLifeHistoryReportDefinition : ReportDefinition<AnimalLifeHistoryReportRow>
{
    private const decimal Tolerance = 1m;

    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IShedRepository _sheds;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IVaccinationRepository _vaccinations;
    private readonly IFinancialTransactionRepository _transactions;
    private readonly IAnimalOverheadAllocationRepository _overheads;
    private readonly IInventoryItemRepository _items;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalValuationService _valuation;
    private readonly ITradeBillRepository _bills;

    public AnimalLifeHistoryReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IShedRepository sheds,
        IAnimalFeedAllocationRepository feed,
        IMedicalTreatmentRepository treatments,
        IVaccinationRepository vaccinations,
        IFinancialTransactionRepository transactions,
        IAnimalOverheadAllocationRepository overheads,
        IInventoryItemRepository items,
        IAnimalCostLedgerRepository ledgers,
        IAnimalValuationService valuation,
        ITradeBillRepository bills)
    {
        _animals = animals;
        _breeds = breeds;
        _sheds = sheds;
        _feed = feed;
        _treatments = treatments;
        _vaccinations = vaccinations;
        _transactions = transactions;
        _overheads = overheads;
        _items = items;
        _ledgers = ledgers;
        _valuation = valuation;
        _bills = bills;
    }

    public override string Key => "finance.animal-life-history";

    public override LocalizedText Title => new("Animal Full History & Profit/Loss", "পশুর সম্পূর্ণ ইতিহাস ও লাভ-ক্ষতি");

    public override LocalizedText Description => new(
        "Everything about one animal from arrival to sale: purchase, weighings, feed month by month, treatments and vaccinations, labour and overhead share, sheds, the sale and payments — with total cost, profit or loss and what it tells you.",
        "একটি পশুর খামারে আসা থেকে বিক্রি পর্যন্ত সবকিছু: ক্রয়, ওজন, মাসভিত্তিক খাদ্য, চিকিৎসা ও টিকা, শ্রম ও পরোক্ষ খরচের অংশ, শেড, বিক্রয় ও পরিশোধ — সাথে মোট খরচ, লাভ বা ক্ষতি ও তার ব্যাখ্যা।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4Portrait;

    public override LocalizedText? GrandTotalLabel => new("Whole life", "সারা জীবন");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each line is one event in the animal's life. Cost is money spent on it; income is money it brought in (the sale).",
            "প্রতিটি সারি পশুটির জীবনের একটি ঘটনা। খরচ হলো এর পেছনে ব্যয়; আয় হলো এর থেকে পাওয়া টাকা (বিক্রয়)।"),
        new("Feed is shown month by month: confirmed feed only, at the stock price on the day it was given.",
            "খাদ্য মাসভিত্তিক দেখানো হয়েছে: শুধু নিশ্চিত করা খাদ্য, দেওয়ার দিনের মজুদ দামে।"),
        new("Labour and overhead are farm-wide bills shared among the animals by the days each was on the farm.",
            "শ্রম ও পরোক্ষ খরচ পুরো খামারের বিল, প্রতিটি পশুর খামারে থাকার দিন অনুযায়ী ভাগ করা।"),
        new("An \"adjustment to match the cost book\" line covers cost booked to the animal without a dated record, so the total equals the animal's cost book.",
            "\"খরচের হিসাবের সাথে মেলানো\" সারিটি তারিখবিহীন খরচ দেখায়, যাতে মোট খরচ পশুটির খরচের হিসাবের সমান হয়।"),
        new("Payments received from the buyer are listed for information; the sale price is counted once, as income.",
            "ক্রেতার পরিশোধ শুধু তথ্যের জন্য দেখানো; বিক্রয়মূল্য একবারই আয় হিসেবে ধরা হয়েছে।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
        ReportParameter.Select(
            "view",
            new LocalizedText("Show history", "ইতিহাস দেখান"),
            [
                new ReportSelectOption("type", new LocalizedText("By kind (feed, health, labour…)", "ধরন অনুযায়ী (খাদ্য, চিকিৎসা, শ্রম…)")),
                new ReportSelectOption("timeline", new LocalizedText("Month by month", "মাস অনুযায়ী")),
            ],
            @default: "type"),
    ];

    public override IReadOnlyList<ReportColumn<AnimalLifeHistoryReportRow>> Columns =>
    [
        ReportColumn<AnimalLifeHistoryReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 20f),
        ReportColumn<AnimalLifeHistoryReportRow>.Text("event", new LocalizedText("What happened", "কী ঘটেছে"), r => r.Event, width: 1.6f),
        ReportColumn<AnimalLifeHistoryReportRow>.Text("details", new LocalizedText("Details", "বিস্তারিত"), r => r.Details, width: 2.6f),
        ReportColumn<AnimalLifeHistoryReportRow>.Money("cost", new LocalizedText("Cost (BDT)", "খরচ (টাকা)"), r => r.CostBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalLifeHistoryReportRow>.Money("income", new LocalizedText("Income (BDT)", "আয় (টাকা)"), r => r.IncomeBdt, decimals: 2, widthMm: 24f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<AnimalLifeHistoryReportRow>? Group =>
        ReportGroup<AnimalLifeHistoryReportRow>.By(r => r.SectionKey, r => r.SectionLabel);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return data is null ? null : AnimalCostReportText.AnimalLine(data.Animal, context.Language, data.Breed);
    }

    protected override async Task<IReadOnlyList<AnimalLifeHistoryReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return data?.Rows ?? [];
    }

    protected override async Task<ReportSummary?> SummarizeAsync(ReportContext context, IReadOnlyList<AnimalLifeHistoryReportRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return data is null ? null : Summarize(context, data.Facts);
    }

    // ── Loading ─────────────────────────────────────────────────────────────

    private sealed record Loaded(Animal Animal, string Breed, IReadOnlyList<AnimalLifeHistoryReportRow> Rows, LifeFacts Facts);

    /// <summary>The figures the summary box is built from.</summary>
    public sealed record LifeFacts(
        string TagId,
        AnimalStatus Status,
        DateOnly ArrivedOn,
        DateOnly? LeftOn,
        int DaysOnFarm,
        AnimalCostBreakdown Cost,
        decimal? SalePriceBdt,
        decimal? EstimatedValueBdt,
        string? BuyerName,
        decimal? FirstWeightKg,
        decimal? EndWeightKg,
        int Weighings,
        decimal FeedKg,
        int FeedDays,
        int Treatments,
        int Vaccinations,
        decimal Last30DaysRunningCostBdt,
        decimal ReceivedBdt,
        decimal OwedBdt,
        decimal UndatedCostBdt);

    private Task<Loaded?> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("animal-life-history", async () =>
        {
            var animal = await _animals.GetByIdWithWeightsAsync(context.Id("animalId"), cancellationToken).ConfigureAwait(false);
            if (animal is null)
                return (Loaded?)null;

            var language = context.Language;
            var bn = language == ReportLanguage.Bn;
            string T(string en, string b) => bn ? b : en;
            string D(DateOnly d) => AnimalCostReportText.Day(d);
            string Kg(decimal v) => v.ToString("#,##0.0", System.Globalization.CultureInfo.InvariantCulture) + T(" kg", " কেজি");
            string Bdt(decimal v) => v.ToString("#,##0.00", System.Globalization.CultureInfo.InvariantCulture) + T(" BDT", " টাকা");

            var timeline = string.Equals(context.Text("view", "type"), "timeline", StringComparison.OrdinalIgnoreCase);
            var breeds = await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var breed = breeds.FirstOrDefault(b => b.Id == animal.BreedId)?.Name ?? AnimalCostReportText.Species(animal.Species, language);

            var ledger = await _ledgers.GetByAnimalIdAsync(animal.Id, cancellationToken).ConfigureAwait(false);
            var feedTotals = await _feed.GetTotalsForAnimalAsync(animal.Id, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);
            var cost = _valuation.CostToDate(animal, ledger, feedTotals.TotalCostBdt);

            var (spanFrom, spanTo) = DailyAnimalCostCalculator.OnFarmSpan(animal);
            var today = context.Today;
            var lastDay = animal.HasLeftFarm ? spanTo : today;
            var to = lastDay < spanFrom ? spanFrom : lastDay;
            var leftOn = animal.ExitDate;
            var daysOnFarm = Math.Max(0, (leftOn ?? today).DayNumber - animal.AcquisitionDate.DayNumber);

            var rows = new List<AnimalLifeHistoryReportRow>();

            // Sections when grouped by kind; month bands when shown as a timeline.
            (string Key, string Label) Section(string kind, DateOnly? date)
            {
                if (timeline)
                {
                    return date is { } d
                        ? (AnimalCostReportText.MonthKey(d), AnimalCostReportText.MonthLabel(d).For(language))
                        : ("9999-99", T("Not dated", "তারিখবিহীন"));
                }

                return kind switch
                {
                    "arrival" => ("1", T("1. Arrival", "১. খামারে আসা")),
                    "weight" => ("2", T("2. Weight & growth", "২. ওজন ও বৃদ্ধি")),
                    "feed" => ("3", T("3. Feed", "৩. খাদ্য")),
                    "health" => ("4", T("4. Health: treatments & vaccinations", "৪. স্বাস্থ্য: চিকিৎসা ও টিকা")),
                    "labour" => ("5", T("5. Labour & overhead share", "৫. শ্রম ও পরোক্ষ খরচের অংশ")),
                    "location" => ("6", T("6. Where it was kept", "৬. কোথায় রাখা হয়েছিল")),
                    _ => ("7", T("7. Sale / leaving the farm", "৭. বিক্রয় / খামার ত্যাগ")),
                };
            }

            void Add(string kind, DateOnly? date, string what, string details, decimal? costBdt = null, decimal? incomeBdt = null)
            {
                var (key, label) = Section(kind, date);
                rows.Add(new AnimalLifeHistoryReportRow(key, label, date, what, details, costBdt, incomeBdt));
            }

            // ── 1. Arrival ──
            Add("arrival", animal.AcquisitionDate,
                animal.AcquisitionType == AcquisitionType.BornOnFarm ? T("Born on the farm", "খামারে জন্ম") : T("Bought", "ক্রয়"),
                $"{breed} · {AnimalCostReportText.Sex(animal.Sex, language)} · {T("born", "জন্ম")} {D(animal.DateOfBirth)}",
                cost.PurchaseBdt);

            // ── 2. Weighings ──
            var weighings = animal.WeightRecords.OrderBy(w => w.RecordedDate).ToList();
            for (var i = 0; i < weighings.Count; i++)
            {
                var w = weighings[i];
                var details = Kg(w.Weight.WeightKg);
                if (i > 0)
                {
                    var prev = weighings[i - 1];
                    var gap = w.RecordedDate.DayNumber - prev.RecordedDate.DayNumber;
                    var change = w.Weight.WeightKg - prev.Weight.WeightKg;
                    details += gap > 0
                        ? $" ({(change >= 0 ? "+" : "")}{Kg(change)} {T("in", "")} {gap} {T("days", "দিনে")}, {(change / gap):0.000} {T("kg/day", "কেজি/দিন")})"
                        : string.Empty;
                }

                Add("weight", w.RecordedDate, i == 0 ? T("First weighing", "প্রথম ওজন") : T("Weighed", "ওজন নেওয়া"), details);
            }

            // ── 3. Feed, month by month ──
            var feedDays = (await _feed.GetDailyEatenByFarmAsync(animal.FarmId, spanFrom, to, cancellationToken).ConfigureAwait(false))
                .Where(d => d.AnimalId == animal.Id)
                .ToList();
            foreach (var month in feedDays.GroupBy(d => new DateOnly(d.Date.Year, d.Date.Month, 1)).OrderBy(g => g.Key))
            {
                var kg = month.Sum(d => d.Kg);
                var bdt = month.Sum(d => d.CostBdt);
                var days = month.Select(d => d.Date).Distinct().Count();
                var rate = kg > 0 ? bdt / kg : 0m;
                Add("feed", month.Max(d => d.Date),
                    T("Feed — ", "খাদ্য — ") + month.Key.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    $"{Kg(kg)} {T("over", "")} {days} {T("days", "দিনে")} · {T("avg", "গড়")} {Kg(days > 0 ? kg / days : 0m)}/{T("day", "দিন")} · {rate:0.00} {T("BDT/kg", "টাকা/কেজি")}",
                    bdt);
            }

            var datedFeed = feedDays.Sum(d => d.CostBdt);
            if (Math.Abs(cost.FeedBdt - datedFeed) > Tolerance)
                Add("feed", null, T("Adjustment to match the cost book", "খরচের হিসাবের সাথে মেলানো"), T("Feed cost booked without a dated feeding record", "তারিখসহ খাদ্য রেকর্ড ছাড়া নথিভুক্ত খাদ্য খরচ"), cost.FeedBdt - datedFeed);

            // ── 4. Health ──
            var treatments = await _treatments.GetByAnimalIdAsync(animal.Id, cancellationToken).ConfigureAwait(false);
            var vaccinations = (await _vaccinations.GetEventsByAnimalIdAsync(animal.Id, cancellationToken).ConfigureAwait(false))
                .Where(v => v.AdministeredDate is not null)
                .ToList();
            var unitCost = new Dictionary<Guid, decimal>();
            foreach (var id in treatments.Where(t => t.InventoryItemId.HasValue).Select(t => t.InventoryItemId!.Value)
                         .Concat(vaccinations.Where(v => v.InventoryItemId.HasValue).Select(v => v.InventoryItemId!.Value))
                         .Distinct())
            {
                unitCost[id] = (await _items.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.WeightedAverageCostBdt ?? 0m;
            }

            var datedVet = 0m;
            foreach (var t in treatments.OrderBy(t => t.StartDate))
            {
                var stockValue = t.InventoryItemId is { } item && t.ConsumptionQuantity is > 0
                    ? Math.Round(t.ConsumptionQuantity.Value * unitCost.GetValueOrDefault(item), 2)
                    : 0m;
                var treatmentCost = Math.Max(t.CostBdt, stockValue);
                datedVet += treatmentCost;
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(t.MedicationName)) parts.Add(t.MedicationName.Trim());
                if (!string.IsNullOrWhiteSpace(t.VeterinarianName)) parts.Add(T("vet ", "পশুচিকিৎসক ") + t.VeterinarianName!.Trim());
                parts.Add(TreatmentStatusText(t.Status, language) + (t.EndDate is { } end ? $" {D(end)}" : string.Empty));
                Add("health", t.StartDate, T("Treatment: ", "চিকিৎসা: ") + (string.IsNullOrWhiteSpace(t.Diagnosis) ? "—" : t.Diagnosis.Trim()), string.Join(" · ", parts), treatmentCost);
            }

            foreach (var v in vaccinations.OrderBy(v => v.AdministeredDate))
            {
                var doseCost = v.InventoryItemId is { } item && v.DosageQuantity is > 0
                    ? Math.Round(v.DosageQuantity.Value * unitCost.GetValueOrDefault(item), 2)
                    : 0m;
                datedVet += doseCost;
                Add("health", v.AdministeredDate, T("Vaccination: ", "টিকা: ") + v.VaccineName,
                    string.IsNullOrWhiteSpace(v.BatchNumber) ? T("Dose given", "ডোজ দেওয়া হয়েছে") : T("Vaccine lot ", "টিকার লট ") + v.BatchNumber,
                    doseCost > 0m ? doseCost : null);
            }

            var manualVet = await _transactions.GetManualVetPostingsAsync(animal.FarmId, animal.AcquisitionDate.AddDays(-366), (leftOn ?? today).AddDays(366), cancellationToken).ConfigureAwait(false);
            foreach (var tx in manualVet.Where(x => x.LedgerVetPostedAnimalId == animal.Id && x.LedgerVetPostedBdt is not null).OrderBy(x => x.TransactionDate))
            {
                datedVet += tx.LedgerVetPostedBdt!.Value;
                Add("health", DateOnly.FromDateTime(tx.TransactionDate), T("Vet / medicine expense", "চিকিৎসা / ওষুধ খরচ"),
                    string.IsNullOrWhiteSpace(tx.Description) ? AccountChart.VoucherNo(tx) : tx.Description.Trim(), tx.LedgerVetPostedBdt.Value);
            }

            if (Math.Abs(cost.VetBdt - datedVet) > Tolerance)
                Add("health", null, T("Adjustment to match the cost book", "খরচের হিসাবের সাথে মেলানো"), T("Vet & medicine cost booked without a dated record", "তারিখসহ রেকর্ড ছাড়া নথিভুক্ত চিকিৎসা খরচ"), cost.VetBdt - datedVet);

            // ── 5. Labour & overhead, one line per month and kind ──
            var shares = await _overheads.GetByAnimalAsync(animal.Id, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false);
            var datedLabour = 0m;
            var datedOverhead = 0m;
            foreach (var g in shares.Where(s => s.AllocatedAmountBdt != 0m)
                         .GroupBy(s => (Month: new DateOnly(s.PeriodStart.Year, s.PeriodStart.Month, 1), s.Bucket))
                         .OrderBy(g => g.Key.Month).ThenBy(g => g.Key.Bucket))
            {
                var amount = g.Sum(s => s.AllocatedAmountBdt);
                var labour = g.Key.Bucket == OverheadBucket.Labour;
                if (labour) datedLabour += amount; else datedOverhead += amount;
                var heads = g.Select(s => AccountChart.For(s.Category)).Select(h => bn ? h.NameBn : h.Name).Distinct().ToList();
                var periodEnd = g.Max(s => s.PeriodEnd);
                var date = leftOn is { } exit && periodEnd >= exit ? exit.AddDays(-1) : periodEnd;
                Add("labour", date < g.Key.Month ? g.Key.Month : date,
                    (labour ? T("Labour share — ", "শ্রমের অংশ — ") : T("Overhead share — ", "পরোক্ষ খরচের অংশ — ")) + g.Key.Month.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture),
                    string.Join(", ", heads.Take(5)) + (heads.Count > 5 ? $" +{heads.Count - 5}" : string.Empty),
                    amount);
            }

            if (Math.Abs(cost.LabourBdt - datedLabour) > Tolerance)
                Add("labour", null, T("Adjustment to match the cost book", "খরচের হিসাবের সাথে মেলানো"), T("Labour booked without a dated share", "তারিখসহ ভাগ ছাড়া নথিভুক্ত শ্রম খরচ"), cost.LabourBdt - datedLabour);
            if (Math.Abs(cost.OverheadBdt - datedOverhead) > Tolerance)
                Add("labour", null, T("Adjustment to match the cost book", "খরচের হিসাবের সাথে মেলানো"), T("Overhead booked without a dated share", "তারিখসহ ভাগ ছাড়া নথিভুক্ত পরোক্ষ খরচ"), cost.OverheadBdt - datedOverhead);

            // ── 6. Sheds ──
            var moves = (await _animals.GetMovementsByFarmAsync(animal.FarmId, cancellationToken).ConfigureAwait(false))
                .Where(m => m.AnimalId == animal.Id)
                .OrderBy(m => m.PlacedAtUtc)
                .ToList();
            if (moves.Count > 0)
            {
                var shedNames = (await _sheds.GetAllByFarmAsync(animal.TenantId, animal.FarmId, cancellationToken).ConfigureAwait(false))
                    .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.ShedName) ? s.ShedNumber : s.ShedName);
                foreach (var m in moves)
                {
                    var name = m.ShedId is { } sid && shedNames.TryGetValue(sid, out var n) ? n : T("no shed", "শেড নেই");
                    var until = m.RemovedAtUtc is { } r ? T(" until ", " থেকে ") + D(DateOnly.FromDateTime(r)) : string.Empty;
                    Add("location", DateOnly.FromDateTime(m.PlacedAtUtc), T("Placed in shed", "শেডে রাখা"), name + until);
                }
            }

            // ── 7. Sale or death ──
            var received = 0m;
            var owed = 0m;
            if (animal.Status == AnimalStatus.Sold)
            {
                var price = animal.SalePriceBdt ?? 0m;
                var parts = new List<string> { string.IsNullOrWhiteSpace(animal.BuyerName) ? T("buyer not recorded", "ক্রেতা লেখা নেই") : T("to ", "ক্রেতা ") + animal.BuyerName!.Trim() };
                if (animal.SaleWeightKg is > 0m and var sw)
                    parts.Add($"{Kg(sw)} · {price / sw:0.00} {T("BDT/kg", "টাকা/কেজি")}");

                var bill = await _bills.FindBySourceAsync(animal.FarmId, animal.Id, TransactionCategory.AnimalSale, cancellationToken).ConfigureAwait(false);
                parts.Add(bill is { IsCancelled: false } ? T("on credit", "বাকিতে") : T("cash", "নগদ"));
                Add("sale", animal.SaleDate, T("Sold", "বিক্রি"), string.Join(" · ", parts), null, price);

                if (bill is { IsCancelled: false })
                {
                    foreach (var p in (await _bills.GetPaymentsAsync(bill.Id, cancellationToken).ConfigureAwait(false)).OrderBy(p => p.PaymentDate))
                    {
                        received += p.AmountBdt;
                        Add("sale", p.PaymentDate, T("Payment received", "পরিশোধ পাওয়া গেছে"), $"{Bdt(p.AmountBdt)} · {p.Method}");
                    }

                    owed = Math.Max(0m, bill.OutstandingBdt);
                    if (owed > 0m)
                        Add("sale", today, T("Still owed by buyer", "ক্রেতার কাছে বাকি"), Bdt(owed));
                }
                else
                {
                    received = price;
                }
            }
            else if (animal.Status is AnimalStatus.Dead or AnimalStatus.Slaughtered)
            {
                Add("sale", animal.DeathDate, animal.Status == AnimalStatus.Dead ? T("Died", "মৃত্যু") : T("Slaughtered", "জবাই"),
                    DisposalText(animal.DisposalReason, language) + (string.IsNullOrWhiteSpace(animal.Notes) ? string.Empty : " · " + animal.Notes!.Trim().Replace(Environment.NewLine, " ", StringComparison.Ordinal)));
            }

            var ordered = rows
                .OrderBy(r => r.SectionKey, StringComparer.Ordinal)
                .ThenBy(r => r.Date ?? DateOnly.MaxValue)
                .ToList();

            var last30From = to.AddDays(-29);
            var last30 = feedDays.Where(d => d.Date >= last30From).Sum(d => d.CostBdt);
            var runningDaily = daysOnFarm > 0 ? (cost.TotalBdt - cost.PurchaseBdt) / daysOnFarm : 0m;

            var endWeight = animal.SaleWeightKg is > 0m ? animal.SaleWeightKg : animal.LatestWeightKg;
            decimal? estimated = animal.HasLeftFarm ? null : _valuation.CurrentValue(animal, ledger, cost.TotalBdt);
            var undated = ordered.Where(r => r.Date is null).Sum(r => r.CostBdt ?? 0m);

            var facts = new LifeFacts(
                animal.Tag.TagId,
                animal.Status,
                animal.AcquisitionDate,
                leftOn,
                daysOnFarm,
                cost,
                animal.Status == AnimalStatus.Sold ? animal.SalePriceBdt ?? 0m : null,
                estimated,
                animal.BuyerName,
                weighings.Count > 0 ? weighings[0].Weight.WeightKg : null,
                endWeight,
                weighings.Count,
                feedDays.Sum(d => d.Kg),
                feedDays.Select(d => d.Date).Distinct().Count(),
                treatments.Count,
                vaccinations.Count,
                last30 > 0m ? last30 : runningDaily * 30m,
                received,
                owed,
                undated);

            return new Loaded(animal, breed, ordered, facts);
        });

    // ── Summary ─────────────────────────────────────────────────────────────

    /// <summary>Headline figures and plain findings for one animal's life on the farm.</summary>
    public static ReportSummary Summarize(ReportContext context, LifeFacts f)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(f);

        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 0, context.BengaliNumerals, context.Language);
        string M2(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals, context.Language);
        string N(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals, context.Language);
        string K(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Number, 1, context.BengaliNumerals, context.Language) + T(" kg", " কেজি");
        string P(decimal ratio) => ReportValueFormatter.Format(ratio, ReportColumnType.Percent, 1, context.BengaliNumerals, context.Language) + "%";
        string Tone(decimal v) => v > 0m ? ReportTone.Good : v < 0m ? ReportTone.Bad : ReportTone.Neutral;
        string Bdt(decimal v) => M(v) + T(" BDT", " টাকা");

        var cost = f.Cost;
        var total = cost.TotalBdt;
        var running = total - cost.PurchaseBdt;
        var sold = f.Status == AnimalStatus.Sold;
        var died = f.LeftOn.HasValue && !sold;
        var value = sold ? f.SalePriceBdt ?? 0m : died ? 0m : f.EstimatedValueBdt ?? 0m;
        var profit = value - total;

        var status = sold
            ? T($"Sold {Day(f.LeftOn)}", $"বিক্রি {Day(f.LeftOn)}")
            : f.Status == AnimalStatus.Dead ? T($"Died {Day(f.LeftOn)}", $"মৃত্যু {Day(f.LeftOn)}")
            : f.Status == AnimalStatus.Slaughtered ? T($"Slaughtered {Day(f.LeftOn)}", $"জবাই {Day(f.LeftOn)}")
            : T("On the farm", "খামারে আছে");

        var figures = new List<ReportSummaryFigure>
        {
            new(T("Status", "অবস্থা"), status),
            new(T("Days on farm", "খামারে দিন"), N(f.DaysOnFarm)),
            new(T("Purchase price", "ক্রয়মূল্য"), Bdt(cost.PurchaseBdt)),
            new(T("Feed", "খাদ্য"), Bdt(cost.FeedBdt)),
            new(T("Vet & medicine", "চিকিৎসা ও ওষুধ"), Bdt(cost.VetBdt)),
            new(T("Labour", "শ্রম"), Bdt(cost.LabourBdt)),
            new(T("Overhead", "পরোক্ষ খরচ"), Bdt(cost.OverheadBdt)),
            new(T("Total cost", "মোট খরচ"), Bdt(total)),
            new(sold ? T("Sold for", "বিক্রয়মূল্য") : died ? T("Value", "মূল্য") : T("Estimated value today", "আজকের আনুমানিক মূল্য"), Bdt(value)),
            new(sold || died
                    ? (profit >= 0m ? T("Profit", "লাভ") : T("Loss", "ক্ষতি"))
                    : (profit >= 0m ? T("Expected profit", "সম্ভাব্য লাভ") : T("Expected loss", "সম্ভাব্য ক্ষতি")),
                Bdt(profit), Tone(profit)),
            new(T("Return on cost", "খরচের ওপর আয়"), total > 0m ? P(profit / total) : "—", Tone(profit)),
            new(T("Profit per day", "দৈনিক লাভ"), f.DaysOnFarm > 0 ? Bdt(profit / f.DaysOnFarm) : "—", Tone(profit)),
        };

        if (f.FirstWeightKg is { } w0 && f.EndWeightKg is { } w1)
            figures.Add(new(T("Weight in → out", "ওজন শুরু → শেষ"), $"{K(w0)} → {K(w1)}"));
        if (sold && f.OwedBdt > 0m)
            figures.Add(new(T("Buyer still owes", "ক্রেতার কাছে বাকি"), Bdt(f.OwedBdt), ReportTone.Bad));

        var insights = new List<string>();

        // The verdict.
        if (sold)
        {
            var verdict = SoldAnimalsProfitReportDefinition.Verdict(cost, value, profit, f.DaysOnFarm, cost.FeedBdt <= 0m && f.DaysOnFarm >= 14, value <= 0m, context.Language);
            insights.Add(profit >= 0m
                ? T($"{f.TagId} was sold for {Bdt(value)} against a total cost of {Bdt(total)}: a profit of {Bdt(profit)}{Margin(profit, value)} — {verdict.ToLowerInvariant()}.",
                    $"{f.TagId} বিক্রি হয়েছে {Bdt(value)} টাকায়, মোট খরচ {Bdt(total)}: লাভ {Bdt(profit)}{Margin(profit, value)} — {verdict}।")
                : T($"{f.TagId} was sold for {Bdt(value)} but cost {Bdt(total)}: a loss of {Bdt(-profit)}. {verdict}.",
                    $"{f.TagId} বিক্রি হয়েছে {Bdt(value)} টাকায় কিন্তু খরচ {Bdt(total)}: ক্ষতি {Bdt(-profit)}। {verdict}।"));
        }
        else if (f.Status == AnimalStatus.Dead)
        {
            insights.Add(T($"{f.TagId} died after {N(f.DaysOnFarm)} days; the whole {Bdt(total)} spent on it is a loss.",
                $"{f.TagId} {N(f.DaysOnFarm)} দিন পর মারা গেছে; এর পেছনে খরচ হওয়া পুরো {Bdt(total)} ক্ষতি।"));
        }
        else if (!f.LeftOn.HasValue)
        {
            insights.Add(profit >= 0m
                ? T($"If sold today at the estimated {Bdt(value)}, {f.TagId} would make about {Bdt(profit)} on the {Bdt(total)} spent so far.",
                    $"আজ আনুমানিক {Bdt(value)} টাকায় বিক্রি করলে {f.TagId}-এ এ পর্যন্ত খরচ {Bdt(total)}-এর ওপর প্রায় {Bdt(profit)} লাভ হবে।")
                : T($"At today's estimated value of {Bdt(value)}, {f.TagId} has not yet covered the {Bdt(total)} spent on it — it is {Bdt(-profit)} short.",
                    $"আজকের আনুমানিক মূল্য {Bdt(value)}, যা {f.TagId}-এর পেছনে খরচ {Bdt(total)} এখনও তোলেনি — {Bdt(-profit)} কম।"));
            insights.Add(T($"To break even it must sell for at least {Bdt(total)}{PerKg(total, f.EndWeightKg)}. Keeping it costs about {Bdt(f.Last30DaysRunningCostBdt / 30m)} a day.",
                $"খরচ তুলতে অন্তত {Bdt(total)}{PerKg(total, f.EndWeightKg)} দামে বিক্রি করতে হবে। এটি রাখতে দৈনিক প্রায় {Bdt(f.Last30DaysRunningCostBdt / 30m)} খরচ হচ্ছে।"));
        }

        // Where the money went.
        if (total > 0m)
        {
            var parts = new (string Name, decimal Amount)[]
            {
                (T("purchase", "ক্রয়"), cost.PurchaseBdt),
                (T("feed", "খাদ্য"), cost.FeedBdt),
                (T("vet & medicine", "চিকিৎসা ও ওষুধ"), cost.VetBdt),
                (T("labour", "শ্রম"), cost.LabourBdt),
                (T("overhead", "পরোক্ষ খরচ"), cost.OverheadBdt),
            };
            insights.Add(T("Where the money went: ", "খরচ কোথায় গেছে: ")
                + string.Join(", ", parts.Where(p => p.Amount != 0m).Select(p => $"{p.Name} {Bdt(p.Amount)} ({P(p.Amount / total)})")) + ".");

            if (running > 0m)
            {
                var biggest = parts.Skip(1).MaxBy(p => p.Amount);
                insights.Add(T($"After the purchase, {biggest.Name} was the biggest cost: {P(biggest.Amount / running)} of the {Bdt(running)} spent on keeping it ({Bdt(f.DaysOnFarm > 0 ? running / f.DaysOnFarm : 0m)} a day).",
                    $"ক্রয়ের পর সবচেয়ে বড় খরচ {biggest.Name}: পালনের মোট {Bdt(running)} খরচের {P(biggest.Amount / running)} (দৈনিক {Bdt(f.DaysOnFarm > 0 ? running / f.DaysOnFarm : 0m)})।"));
            }
        }

        // Feed and growth.
        if (f.FeedKg > 0m && f.FeedDays > 0)
        {
            insights.Add(T($"It ate {K(f.FeedKg)} of feed over {N(f.FeedDays)} fed days — {K(f.FeedKg / f.FeedDays)} a day on average.",
                $"{N(f.FeedDays)} দিনে মোট {K(f.FeedKg)} খাদ্য খেয়েছে — দৈনিক গড়ে {K(f.FeedKg / f.FeedDays)}।"));
        }

        if (f.FirstWeightKg is { } first && f.EndWeightKg is { } end && f.Weighings > 0)
        {
            var gain = end - first;
            if (gain > 0m)
            {
                var gainDays = Math.Max(1, f.DaysOnFarm);
                var feedPerKg = cost.FeedBdt / gain;
                insights.Add(T($"It gained {K(gain)} ({K(first)} → {K(end)}), about {M2(gain / gainDays)} kg a day. Each kg gained cost {Bdt(feedPerKg)} in feed and {Bdt(running / gain)} in all running costs.",
                    $"ওজন বেড়েছে {K(gain)} ({K(first)} → {K(end)}), দৈনিক প্রায় {M2(gain / gainDays)} কেজি। প্রতি কেজি বৃদ্ধিতে খাদ্য খরচ {Bdt(feedPerKg)} এবং মোট পালন খরচ {Bdt(running / gain)}।"));
            }
            else
            {
                insights.Add(T($"It did not gain weight on the farm ({K(first)} → {K(end)}) — check its health and ration.",
                    $"খামারে এর ওজন বাড়েনি ({K(first)} → {K(end)}) — স্বাস্থ্য ও খাদ্য তালিকা যাচাই করুন।"));
            }
        }

        if (sold && f.EndWeightKg is > 0m && value > 0m)
        {
            var kg = f.EndWeightKg.Value;
            insights.Add(value >= total
                ? T($"It sold at {M2(value / kg)} BDT/kg live weight; break-even was {M2(total / kg)} BDT/kg.",
                    $"প্রতি কেজি জীবিত ওজন {M2(value / kg)} টাকায় বিক্রি; খরচ তোলার দর ছিল {M2(total / kg)} টাকা/কেজি।")
                : T($"It sold at {M2(value / kg)} BDT/kg live weight, below the break-even of {M2(total / kg)} BDT/kg.",
                    $"প্রতি কেজি জীবিত ওজন {M2(value / kg)} টাকায় বিক্রি, যা খরচ তোলার দর {M2(total / kg)} টাকা/কেজির কম।"));
        }

        if (f.Treatments + f.Vaccinations > 0)
        {
            insights.Add(T($"Health: {N(f.Treatments)} treatment(s) and {N(f.Vaccinations)} vaccination(s), costing {Bdt(cost.VetBdt)} in all.",
                $"স্বাস্থ্য: {N(f.Treatments)}টি চিকিৎসা ও {N(f.Vaccinations)}টি টিকা, মোট খরচ {Bdt(cost.VetBdt)}।"));
        }

        if (sold && f.OwedBdt > 0m)
        {
            insights.Add(T($"The buyer has paid {Bdt(f.ReceivedBdt)} and still owes {Bdt(f.OwedBdt)}.",
                $"ক্রেতা {Bdt(f.ReceivedBdt)} দিয়েছেন, এখনও {Bdt(f.OwedBdt)} বাকি।"));
        }

        // Records that make the figures less reliable.
        if (cost.FeedBdt <= 0m && f.DaysOnFarm >= 14)
            insights.Add(T("Check: no feed cost is recorded for this animal, so its profit is shown too high.",
                "যাচাই করুন: এই পশুর কোনো খাদ্য খরচ নথিভুক্ত নেই, তাই লাভ বেশি দেখাচ্ছে।"));
        if (f.Weighings == 0)
            insights.Add(T("Check: it was never weighed, so growth and cost per kg cannot be worked out.",
                "যাচাই করুন: কখনো ওজন নেওয়া হয়নি, তাই বৃদ্ধি ও প্রতি কেজি খরচ হিসাব করা যায়নি।"));
        if (Math.Abs(f.UndatedCostBdt) > 1m)
            insights.Add(T($"{Bdt(f.UndatedCostBdt)} of its cost is in the cost book without a dated record; it is shown as an adjustment line.",
                $"এর {Bdt(f.UndatedCostBdt)} খরচ তারিখসহ রেকর্ড ছাড়া খরচের হিসাবে আছে; এটি মেলানোর সারি হিসেবে দেখানো হয়েছে।"));

        return new ReportSummary(T("Summary", "সারসংক্ষেপ"), figures, T("What it tells us", "যা বোঝা যাচ্ছে"), insights);

        string Margin(decimal p, decimal v) => v > 0m ? T($" ({P(p / v)} margin)", $" ({P(p / v)} মার্জিন)") : string.Empty;
        string PerKg(decimal amount, decimal? kg) => kg is > 0m ? T($" ({M2(amount / kg.Value)} BDT/kg at {K(kg.Value)})", $" ({K(kg.Value)} ওজনে {M2(amount / kg.Value)} টাকা/কেজি)") : string.Empty;
        string Day(DateOnly? d) => d is { } x ? AnimalCostReportText.Day(x) : string.Empty;
    }

    private static string TreatmentStatusText(TreatmentStatus status, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return status switch
        {
            TreatmentStatus.Ongoing => bn ? "চলমান" : "ongoing",
            TreatmentStatus.Completed => bn ? "শেষ" : "completed",
            TreatmentStatus.Failed => bn ? "ব্যর্থ" : "did not work",
            TreatmentStatus.Referred => bn ? "অন্যত্র পাঠানো" : "referred",
            _ => status.ToString(),
        };
    }

    private static string DisposalText(DisposalReason? reason, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return reason switch
        {
            DisposalReason.NaturalDeath => bn ? "স্বাভাবিক মৃত্যু" : "Natural death",
            DisposalReason.Disease => bn ? "রোগে মৃত্যু" : "Disease",
            DisposalReason.Accident => bn ? "দুর্ঘটনা" : "Accident",
            DisposalReason.Slaughter => bn ? "জবাই" : "Slaughter",
            _ => bn ? "কারণ জানা নেই" : "Cause not known",
        };
    }
}
