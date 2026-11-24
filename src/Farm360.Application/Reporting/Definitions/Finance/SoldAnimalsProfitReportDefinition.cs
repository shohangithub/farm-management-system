using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One sold animal: what it cost by kind, what it fetched, the profit and a one-line verdict.</summary>
public sealed record SoldAnimalProfitReportRow(
    string GroupKey,
    string GroupLabel,
    string Animal,
    string TagId,
    string? BatchName,
    DateOnly SaleDate,
    int DaysKept,
    decimal? SaleWeightKg,
    decimal? WeightGainKg,
    decimal PurchaseBdt,
    decimal FeedBdt,
    decimal VetBdt,
    decimal LabourOverheadBdt,
    decimal TotalCostBdt,
    decimal SalePriceBdt,
    decimal ProfitBdt,
    decimal? Margin,
    decimal? ProfitPerDayBdt,
    string Verdict,
    bool NoFeedRecorded,
    bool NoPriceRecorded,
    decimal OwedBdt);

/// <summary>
/// Report FN13 — profit and loss on every animal sold in the period. Each animal's full cost
/// (purchase, feed, vet &amp; medicine, its share of labour and overhead up to the day it left) is
/// set against its sale price, with margin, profit per day kept and a plain verdict on why it made
/// or lost money. The summary box above the table gives the headline figures and findings: best
/// and worst sale, where the money went, whether keeping animals longer paid, break-even price per
/// kg, and records that are missing and make a profit look better than it is.
/// </summary>
/// <remarks>
/// Cost is <see cref="IAnimalValuationService.CostToDate"/>, the figure the Sales Register, Animal
/// Cost to Date and herd reports use, so the profit here agrees with them to the taka.
/// </remarks>
public sealed class SoldAnimalsProfitReportDefinition : ReportDefinition<SoldAnimalProfitReportRow>
{
    private const decimal StrongMargin = 0.20m;
    private const decimal GoodMargin = 0.08m;

    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IAnimalBatchRepository _batches;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalValuationService _valuation;
    private readonly ITradeBillRepository _bills;

    public SoldAnimalsProfitReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IAnimalBatchRepository batches,
        IAnimalFeedAllocationRepository feed,
        IAnimalCostLedgerRepository ledgers,
        IAnimalValuationService valuation,
        ITradeBillRepository bills)
    {
        _animals = animals;
        _breeds = breeds;
        _batches = batches;
        _feed = feed;
        _ledgers = ledgers;
        _valuation = valuation;
        _bills = bills;
    }

    public override string Key => "finance.sold-animals-profit";

    public override LocalizedText Title => new("Sold Animals — Profit & Loss", "বিক্রিত পশু — লাভ ও ক্ষতি");

    public override LocalizedText Description => new(
        "Every animal sold in the period: everything it cost (purchase, feed, treatment, labour and overhead) against what it sold for, the profit or loss, and why — with a summary of the best and worst sales and what to change.",
        "নির্বাচিত সময়ে বিক্রি হওয়া প্রতিটি পশু: মোট খরচ (ক্রয়, খাদ্য, চিকিৎসা, শ্রম ও পরোক্ষ খরচ) বনাম বিক্রয়মূল্য, লাভ বা ক্ষতি ও তার কারণ — সাথে সেরা ও দুর্বল বিক্রয় এবং করণীয়ের সারসংক্ষেপ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("All animals sold", "সকল বিক্রিত পশু");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("One row per animal sold. Total cost = purchase price + feed + vet & medicine + its share of labour and overhead, up to the day before it was sold.",
            "প্রতিটি বিক্রিত পশুর জন্য একটি সারি। মোট খরচ = ক্রয়মূল্য + খাদ্য + চিকিৎসা ও ওষুধ + শ্রম ও পরোক্ষ খরচের অংশ, বিক্রির আগের দিন পর্যন্ত।"),
        new("Profit / (loss) = sale price − total cost; a minus figure is a loss.",
            "লাভ / (ক্ষতি) = বিক্রয়মূল্য − মোট খরচ; বিয়োগ চিহ্ন মানে ক্ষতি।"),
        new("Margin = profit ÷ sale price. Profit per day = profit ÷ days the animal was on the farm.",
            "মার্জিন = লাভ ÷ বিক্রয়মূল্য। দৈনিক লাভ = লাভ ÷ খামারে থাকার দিন।"),
        new("Verdict: strong profit is a margin of 20% or more, good profit 8–20%, thin profit below 8%; for a loss it names the biggest reason.",
            "মন্তব্য: ২০% বা বেশি মার্জিন = ভালো লাভ, ৮–২০% = মোটামুটি লাভ, ৮%-এর কম = সামান্য লাভ; ক্ষতির ক্ষেত্রে প্রধান কারণ লেখা আছে।"),
        new("An animal with no feed cost recorded shows a profit that is too high — record its feed and run the report again.",
            "যে পশুর খাদ্য খরচ নথিভুক্ত নেই তার লাভ বেশি দেখাবে — খাদ্যের হিসাব লিখে আবার প্রতিবেদনটি চালান।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-year"),
        ReportParameter.Batch(name: "batchId", required: false),
        ReportParameter.Select(
            "groupBy",
            new LocalizedText("Group rows by", "যেভাবে ভাগ করা হবে"),
            [
                new ReportSelectOption("result", new LocalizedText("Profit or loss", "লাভ বা ক্ষতি")),
                new ReportSelectOption("month", new LocalizedText("Month sold", "বিক্রির মাস")),
                new ReportSelectOption("batch", new LocalizedText("Batch", "ব্যাচ")),
            ],
            @default: "result"),
    ];

    public override IReadOnlyList<ReportColumn<SoldAnimalProfitReportRow>> Columns =>
    [
        ReportColumn<SoldAnimalProfitReportRow>.Text("animal", new LocalizedText("Animal (tag · breed)", "পশু (ট্যাগ · জাত)"), r => r.Animal, width: 1.6f),
        ReportColumn<SoldAnimalProfitReportRow>.Date("saleDate", new LocalizedText("Sold on", "বিক্রির তারিখ"), r => r.SaleDate, widthMm: 19f),
        ReportColumn<SoldAnimalProfitReportRow>.WholeNumber("days", new LocalizedText("Days kept", "খামারে দিন"), r => r.DaysKept, widthMm: 12f),
        ReportColumn<SoldAnimalProfitReportRow>.Number("weight", new LocalizedText("Sale weight (kg)", "বিক্রয় ওজন (কেজি)"), r => r.SaleWeightKg, 1, 15f, ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("purchase", new LocalizedText("Purchase price (BDT)", "ক্রয়মূল্য (টাকা)"), r => r.PurchaseBdt, decimals: 0, widthMm: 19f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("feed", new LocalizedText("Feed (BDT)", "খাদ্য (টাকা)"), r => r.FeedBdt, decimals: 0, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("vet", new LocalizedText("Vet & medicine (BDT)", "চিকিৎসা ও ওষুধ (টাকা)"), r => r.VetBdt, decimals: 0, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("labour", new LocalizedText("Labour & overhead (BDT)", "শ্রম ও পরোক্ষ (টাকা)"), r => r.LabourOverheadBdt, decimals: 0, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("cost", new LocalizedText("Total cost (BDT)", "মোট খরচ (টাকা)"), r => r.TotalCostBdt, decimals: 0, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("price", new LocalizedText("Sale price (BDT)", "বিক্রয়মূল্য (টাকা)"), r => r.SalePriceBdt, decimals: 0, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Money("profit", new LocalizedText("Profit / (loss) (BDT)", "লাভ / (ক্ষতি) (টাকা)"), r => r.ProfitBdt, decimals: 0, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<SoldAnimalProfitReportRow>.Percent("margin", new LocalizedText("Margin (%)", "মার্জিন (%)"), r => r.Margin, decimals: 1, widthMm: 13f),
        ReportColumn<SoldAnimalProfitReportRow>.Money("perDay", new LocalizedText("Profit per day (BDT)", "দৈনিক লাভ (টাকা)"), r => r.ProfitPerDayBdt, decimals: 0, widthMm: 15f),
        ReportColumn<SoldAnimalProfitReportRow>.Text("verdict", new LocalizedText("Verdict", "মন্তব্য"), r => r.Verdict, width: 2.0f),
    ];

    public override ReportGroup<SoldAnimalProfitReportRow>? Group =>
        ReportGroup<SoldAnimalProfitReportRow>.By(r => r.GroupKey, r => r.GroupLabel);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var period = AnimalCostReportText.Period(context.Range("period"));
        var batch = await BatchNameAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        var parts = new List<string> { (bn ? "বিক্রি হয়েছে " : "Sold ") + period };
        if (batch is not null)
            parts.Add((bn ? "ব্যাচ " : "Batch ") + batch);
        return string.Join(" · ", parts);
    }

    protected override Task<IReadOnlyList<SoldAnimalProfitReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LoadAsync(context, cancellationToken);
    }

    // ── Rows ────────────────────────────────────────────────────────────────

    private Task<IReadOnlyList<SoldAnimalProfitReportRow>> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync<IReadOnlyList<SoldAnimalProfitReportRow>>("sold-animals-profit", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var batchFilter = context.IdOrNull("batchId");
            var language = context.Language;

            var sold = (await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false))
                .Where(a => a.Status == AnimalStatus.Sold && a.SaleDate is { } d && period.Contains(d))
                .Where(a => batchFilter is null || a.BatchId == batchFilter)
                .ToList();
            if (sold.Count == 0)
                return [];

            var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(b => b.Id, b => b.Name);
            var feed = (await _feed.GetTotalsForFarmAsync(farmId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false))
                .ToDictionary(t => t.AnimalId);
            var ledgers = (await _ledgers.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false))
                .GroupBy(l => l.AnimalId)
                .ToDictionary(g => g.Key, g => g.First());
            var firstWeights = (await _animals.GetWeighInsByFarmAsync(farmId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false))
                .GroupBy(w => w.AnimalId)
                .ToDictionary(g => g.Key, g => g.OrderBy(w => w.Date).ToList());
            var bills = (await _bills.GetByFarmIdAsync(farmId, TradeBillKind.Receivable, includeCancelled: false, cancellationToken).ConfigureAwait(false))
                .Where(b => b.Category == TransactionCategory.AnimalSale)
                .GroupBy(b => b.SourceDocumentId)
                .ToDictionary(g => g.Key, g => g.First());

            var batchNames = new Dictionary<Guid, string>();
            foreach (var id in sold.Where(a => a.BatchId is not null).Select(a => a.BatchId!.Value).Distinct())
            {
                if (await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false) is { } b)
                    batchNames[id] = b.Name;
            }

            var groupBy = context.Text("groupBy", "result");
            var rows = new List<SoldAnimalProfitReportRow>(sold.Count);

            foreach (var animal in sold)
            {
                ledgers.TryGetValue(animal.Id, out var ledger);
                var cost = _valuation.CostToDate(animal, ledger, feed.TryGetValue(animal.Id, out var f) ? f.TotalCostBdt : 0m);
                var saleDate = animal.SaleDate!.Value;
                var days = Math.Max(0, saleDate.DayNumber - animal.AcquisitionDate.DayNumber);
                var price = animal.SalePriceBdt ?? 0m;
                var profit = price - cost.TotalBdt;
                var weight = animal.SaleWeightKg is > 0m ? animal.SaleWeightKg : null;
                var gain = WeightGain(animal, weight, firstWeights.TryGetValue(animal.Id, out var w) ? w : null);
                var batch = animal.BatchId is { } bid && batchNames.TryGetValue(bid, out var bname) ? bname : null;
                var breed = breeds.TryGetValue(animal.BreedId, out var br) ? br : AnimalCostReportText.Species(animal.Species, language);
                var noFeed = cost.FeedBdt <= 0m && days >= 14;
                var noPrice = animal.SalePriceBdt is not > 0m;
                var owed = bills.TryGetValue(animal.Id, out var bill) && !bill.IsCancelled ? Math.Max(0m, bill.OutstandingBdt) : 0m;

                var (key, label) = GroupFor(groupBy, profit, saleDate, batch, language);

                rows.Add(new SoldAnimalProfitReportRow(
                    key,
                    label,
                    $"{animal.Tag.TagId} · {breed}",
                    animal.Tag.TagId,
                    batch,
                    saleDate,
                    days,
                    weight,
                    gain,
                    cost.PurchaseBdt,
                    cost.FeedBdt,
                    cost.VetBdt,
                    cost.LabourBdt + cost.OverheadBdt,
                    cost.TotalBdt,
                    price,
                    profit,
                    price > 0m ? decimal.Round(profit / price, 4, MidpointRounding.AwayFromZero) : null,
                    days > 0 ? decimal.Round(profit / days, 2, MidpointRounding.AwayFromZero) : null,
                    Verdict(cost, price, profit, days, noFeed, noPrice, language),
                    noFeed,
                    noPrice,
                    owed));
            }

            return rows
                .OrderBy(r => r.GroupKey, StringComparer.Ordinal)
                .ThenByDescending(r => groupBy == "result" ? r.ProfitBdt : 0m)
                .ThenBy(r => r.SaleDate)
                .ThenBy(r => r.TagId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });

    private static decimal? WeightGain(Animal animal, decimal? saleWeight, List<(Guid AnimalId, DateOnly Date, decimal WeightKg)>? weighIns)
    {
        if (weighIns is null || weighIns.Count == 0)
            return null;

        var first = weighIns[0];
        var end = saleWeight
            ?? weighIns.Where(x => x.Date <= animal.SaleDate).Select(x => (decimal?)x.WeightKg).LastOrDefault();
        if (end is null || first.Date >= animal.SaleDate || (saleWeight is null && weighIns.Count < 2))
            return null;

        return end.Value - first.WeightKg;
    }

    private static (string Key, string Label) GroupFor(string groupBy, decimal profit, DateOnly saleDate, string? batch, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        switch (groupBy)
        {
            case "month":
                return (AnimalCostReportText.MonthKey(saleDate), AnimalCostReportText.MonthLabel(saleDate).For(language));
            case "batch":
                return batch is null
                    ? ("~", bn ? "কোনো ব্যাচে নেই" : "Not in a batch")
                    : ("b:" + batch, (bn ? "ব্যাচ: " : "Batch: ") + batch);
            default:
                return profit >= 0m
                    ? ("1", bn ? "লাভে বিক্রি" : "Sold at a profit")
                    : ("2", bn ? "লোকসানে বিক্রি" : "Sold at a loss");
        }
    }

    /// <summary>A short reason a farmer can act on: how good the profit was, or what caused the loss.</summary>
    internal static string Verdict(AnimalCostBreakdown cost, decimal price, decimal profit, int days, bool noFeed, bool noPrice, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;

        if (noPrice)
            return T("Sale price not recorded", "বিক্রয়মূল্য লেখা হয়নি");

        string verdict;
        if (profit >= 0m)
        {
            var margin = profit / price;
            verdict = margin >= StrongMargin ? T("Strong profit", "ভালো লাভ")
                : margin >= GoodMargin ? T("Good profit", "মোটামুটি লাভ")
                : T("Thin profit — little room for error", "সামান্য লাভ — ঝুঁকি বেশি");
        }
        else
        {
            var running = cost.TotalBdt - cost.PurchaseBdt;
            verdict = price < cost.PurchaseBdt ? T("Sold for less than it was bought for", "কেনা দামের চেয়েও কমে বিক্রি")
                : cost.TotalBdt > 0m && cost.VetBdt / cost.TotalBdt >= 0.15m ? T("Loss — treatment costs were heavy", "ক্ষতি — চিকিৎসা খরচ বেশি")
                : days > 365 ? T("Loss — kept too long, costs kept adding up", "ক্ষতি — বেশি দিন রাখায় খরচ বেড়েছে")
                : running > 0m && cost.FeedBdt / running >= 0.6m ? T("Loss — feed cost too high for the price", "ক্ষতি — দামের তুলনায় খাদ্য খরচ বেশি")
                : T("Loss — price did not cover its costs", "ক্ষতি — দামে খরচ ওঠেনি");
        }

        if (noFeed)
            verdict += T(" · no feed cost recorded, check", " · খাদ্য খরচ নেই, যাচাই করুন");

        return verdict;
    }

    // ── Summary box ─────────────────────────────────────────────────────────

    protected override Task<ReportSummary?> SummarizeAsync(ReportContext context, IReadOnlyList<SoldAnimalProfitReportRow> rows, CancellationToken cancellationToken) =>
        Task.FromResult<ReportSummary?>(Summarize(context, rows));

    /// <summary>Headline figures and plain findings for the sold animals in <paramref name="rows"/>.</summary>
    public static ReportSummary Summarize(ReportContext context, IReadOnlyList<SoldAnimalProfitReportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rows);

        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 0, context.BengaliNumerals, context.Language);
        string M2(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals, context.Language);
        string N(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals, context.Language);
        string P(decimal ratio) => ReportValueFormatter.Format(ratio, ReportColumnType.Percent, 1, context.BengaliNumerals, context.Language) + "%";
        string Tone(decimal v) => v > 0m ? ReportTone.Good : v < 0m ? ReportTone.Bad : ReportTone.Neutral;
        string Bdt(decimal v) => M(v) + T(" BDT", " টাকা");

        var priced = rows.Where(r => !r.NoPriceRecorded).ToList();
        var count = rows.Count;
        var sales = rows.Sum(r => r.SalePriceBdt);
        var cost = rows.Sum(r => r.TotalCostBdt);
        var profit = rows.Sum(r => r.ProfitBdt);
        var winners = priced.Where(r => r.ProfitBdt >= 0m).ToList();
        var losers = priced.Where(r => r.ProfitBdt < 0m).ToList();
        var totalDays = rows.Sum(r => r.DaysKept);
        var owed = rows.Sum(r => r.OwedBdt);
        var weighed = priced.Where(r => r.SaleWeightKg is > 0m).ToList();
        var weighedKg = weighed.Sum(r => r.SaleWeightKg!.Value);

        // ── Headline figures ──
        var figures = new List<ReportSummaryFigure>
        {
            new(T("Animals sold", "বিক্রিত পশু"),
                $"{N(count)}  ({N(winners.Count)} {T("profit", "লাভ")} · {N(losers.Count)} {T("loss", "ক্ষতি")})"),
            new(T("Total sales", "মোট বিক্রয়"), Bdt(sales)),
            new(T("Total cost", "মোট খরচ"), Bdt(cost)),
            new(profit >= 0m ? T("Net profit", "নিট লাভ") : T("Net loss", "নিট ক্ষতি"), Bdt(profit), Tone(profit)),
            new(T("Margin on sales", "বিক্রয়ে মার্জিন"), sales > 0m ? P(profit / sales) : "—", Tone(profit)),
            new(T("Return on money spent", "খরচের ওপর আয়"), cost > 0m ? P(profit / cost) : "—", Tone(profit)),
            new(T("Average profit per animal", "পশুপ্রতি গড় লাভ"), Bdt(count > 0 ? profit / count : 0m), Tone(profit)),
            new(T("Average days kept", "গড় খামারে দিন"), count > 0 ? N((decimal)totalDays / count) : "—"),
            new(T("Profit per animal per day", "পশুপ্রতি দৈনিক লাভ"), totalDays > 0 ? Bdt(profit / totalDays) : "—", Tone(profit)),
        };

        if (weighedKg > 0m)
        {
            var pricePerKg = weighed.Sum(r => r.SalePriceBdt) / weighedKg;
            var costPerKg = weighed.Sum(r => r.TotalCostBdt) / weighedKg;
            figures.Add(new(T("Sold at / cost per kg", "প্রতি কেজি বিক্রয় / খরচ"),
                $"{M2(pricePerKg)} / {M2(costPerKg)}", Tone(pricePerKg - costPerKg)));
        }

        if (owed > 0m)
            figures.Add(new(T("Buyers still owe", "ক্রেতার কাছে বাকি"), Bdt(owed), ReportTone.Bad));

        // ── Findings ──
        var insights = new List<string>();

        insights.Add(profit >= 0m
            ? T($"Overall the {N(count)} animals made {Bdt(profit)} profit — {M(cost > 0m ? profit / cost * 100m : 0m)} BDT earned for every 100 BDT spent.",
                $"সব মিলিয়ে {N(count)}টি পশুতে {Bdt(profit)} লাভ — প্রতি ১০০ টাকা খরচে {M(cost > 0m ? profit / cost * 100m : 0m)} টাকা আয়।")
            : T($"Overall the {N(count)} animals lost {Bdt(-profit)} — the sales did not cover what they cost.",
                $"সব মিলিয়ে {N(count)}টি পশুতে {Bdt(-profit)} ক্ষতি — বিক্রয়মূল্যে খরচ ওঠেনি।"));

        if (priced.Count > 0)
        {
            var best = priced.MaxBy(r => r.ProfitBdt)!;
            if (best.ProfitBdt > 0m)
            {
                insights.Add(T(
                    $"Best sale: {best.TagId} made {Bdt(best.ProfitBdt)}{MarginText(best)} after {N(best.DaysKept)} days.",
                    $"সেরা বিক্রয়: {best.TagId} — {N(best.DaysKept)} দিনে {Bdt(best.ProfitBdt)} লাভ{MarginText(best)}।"));
            }

            if (losers.Count > 0)
            {
                var worst = losers.MinBy(r => r.ProfitBdt)!;
                insights.Add(T(
                    $"Biggest loss: {worst.TagId} lost {Bdt(-worst.ProfitBdt)} — {StripCheck(worst.Verdict)}.",
                    $"সবচেয়ে বড় ক্ষতি: {worst.TagId} — {Bdt(-worst.ProfitBdt)} ক্ষতি ({StripCheck(worst.Verdict)})।"));

                var gap = -losers.Sum(r => r.ProfitBdt);
                insights.Add(T(
                    $"{N(losers.Count)} animal(s) sold at a loss; together they needed {Bdt(gap)} more ({Bdt(gap / losers.Count)} each on average) just to break even.",
                    $"{N(losers.Count)}টি পশু লোকসানে বিক্রি; খরচ তুলতে মোট আরও {Bdt(gap)} (গড়ে প্রতিটিতে {Bdt(gap / losers.Count)}) বেশি দামে বিক্রি করা দরকার ছিল।"));
            }
            else if (priced.Count > 1)
            {
                var weakest = priced.MinBy(r => r.ProfitBdt)!;
                insights.Add(T(
                    $"No animal was sold at a loss. Weakest sale: {weakest.TagId} with {Bdt(weakest.ProfitBdt)}{MarginText(weakest)}.",
                    $"কোনো পশু লোকসানে বিক্রি হয়নি। সবচেয়ে কম লাভ: {weakest.TagId} — {Bdt(weakest.ProfitBdt)}{MarginText(weakest)}।"));
            }
        }

        // Where the money went.
        if (cost > 0m)
        {
            decimal Share(decimal v) => v / cost;
            var purchase = rows.Sum(r => r.PurchaseBdt);
            var feed = rows.Sum(r => r.FeedBdt);
            var vet = rows.Sum(r => r.VetBdt);
            var labour = rows.Sum(r => r.LabourOverheadBdt);
            insights.Add(T(
                $"Where the money went: purchase {P(Share(purchase))}, feed {P(Share(feed))}, vet & medicine {P(Share(vet))}, labour & overhead {P(Share(labour))}.",
                $"খরচ কোথায় গেছে: ক্রয় {P(Share(purchase))}, খাদ্য {P(Share(feed))}, চিকিৎসা ও ওষুধ {P(Share(vet))}, শ্রম ও পরোক্ষ {P(Share(labour))}।"));

            var running = feed + vet + labour;
            if (purchase > 0m && running > 0m)
            {
                var margin = sales - purchase;
                insights.Add(margin >= running
                    ? T($"Sale prices were {Bdt(margin)} above purchase prices, which covered the {Bdt(running)} spent on keeping them.",
                        $"বিক্রয়মূল্য ক্রয়মূল্যের চেয়ে {Bdt(margin)} বেশি, যা পালনের {Bdt(running)} খরচ মিটিয়েছে।")
                    : T($"Sale prices were only {Bdt(margin)} above purchase prices, but keeping the animals cost {Bdt(running)} — buy cheaper, feed more efficiently or sell heavier.",
                        $"বিক্রয়মূল্য ক্রয়মূল্যের চেয়ে মাত্র {Bdt(margin)} বেশি, অথচ পালনে খরচ {Bdt(running)} — কম দামে কিনুন, খাদ্য সাশ্রয় করুন বা বেশি ওজনে বিক্রি করুন।"));
            }
        }

        // Did keeping them longer pay?
        if (priced.Count >= 4)
        {
            var ordered = priced.OrderBy(r => r.DaysKept).ToList();
            var cut = ordered[ordered.Count / 2 - 1].DaysKept;
            var shortKept = ordered.Where(r => r.DaysKept <= cut).ToList();
            var longKept = ordered.Where(r => r.DaysKept > cut).ToList();
            var shortDays = shortKept.Sum(r => r.DaysKept);
            var longDays = longKept.Sum(r => r.DaysKept);
            if (shortKept.Count > 0 && longKept.Count > 0 && shortDays > 0 && longDays > 0)
            {
                var shortRate = shortKept.Sum(r => r.ProfitBdt) / shortDays;
                var longRate = longKept.Sum(r => r.ProfitBdt) / longDays;
                var advice = longRate >= shortRate
                    ? T("keeping them longer paid off.", "বেশি দিন রাখা লাভজনক হয়েছে।")
                    : T("selling earlier would have earned more per day.", "আগে বিক্রি করলে দৈনিক লাভ বেশি হতো।");
                insights.Add(T(
                    $"Animals kept up to {N(cut)} days earned {Bdt(shortRate)} a day each; those kept longer earned {Bdt(longRate)} a day — {advice}",
                    $"{N(cut)} দিন পর্যন্ত রাখা পশু দৈনিক {Bdt(shortRate)} এবং এর বেশি দিন রাখা পশু দৈনিক {Bdt(longRate)} আয় করেছে — {advice}"));
            }
        }

        // Price per kg against break-even.
        if (weighedKg > 0m)
        {
            var pricePerKg = weighed.Sum(r => r.SalePriceBdt) / weighedKg;
            var costPerKg = weighed.Sum(r => r.TotalCostBdt) / weighedKg;
            insights.Add(pricePerKg >= costPerKg
                ? T($"They sold at {M2(pricePerKg)} BDT per kg live weight; break-even was {M2(costPerKg)} BDT per kg, so each kg earned {M2(pricePerKg - costPerKg)} BDT.",
                    $"প্রতি কেজি জীবিত ওজন {M2(pricePerKg)} টাকায় বিক্রি; খরচ তোলার দর ছিল {M2(costPerKg)} টাকা, তাই প্রতি কেজিতে {M2(pricePerKg - costPerKg)} টাকা লাভ।")
                : T($"They sold at {M2(pricePerKg)} BDT per kg live weight, below the break-even of {M2(costPerKg)} BDT per kg — ask at least {M2(costPerKg)} BDT/kg next time.",
                    $"প্রতি কেজি জীবিত ওজন {M2(pricePerKg)} টাকায় বিক্রি, যা খরচ তোলার দর {M2(costPerKg)} টাকার কম — পরের বার অন্তত {M2(costPerKg)} টাকা/কেজি দাম চান।"));

            var gained = weighed.Where(r => r.WeightGainKg is > 0m && r.DaysKept > 0).ToList();
            if (gained.Count > 0)
            {
                var adg = gained.Sum(r => r.WeightGainKg!.Value) / gained.Sum(r => r.DaysKept);
                insights.Add(T(
                    $"Average weight gain while on the farm: {M2(adg)} kg per day (from {N(gained.Count)} animals weighed on arrival).",
                    $"খামারে থাকাকালীন গড় ওজন বৃদ্ধি: দৈনিক {M2(adg)} কেজি ({N(gained.Count)}টি পশুর হিসাবে)।"));
            }
        }

        if (owed > 0m)
        {
            insights.Add(T($"Buyers still owe {Bdt(owed)} on these sales — the profit is not all in hand yet.",
                $"এই বিক্রয়গুলোর {Bdt(owed)} এখনও ক্রেতার কাছে বাকি — সব লাভ এখনও হাতে আসেনি।"));
        }

        // Records that make the result unreliable.
        var noFeed = rows.Where(r => r.NoFeedRecorded).Select(r => r.TagId).ToList();
        if (noFeed.Count > 0)
        {
            insights.Add(T(
                $"Check: {N(noFeed.Count)} animal(s) have no feed cost recorded ({TagList(noFeed)}), so their profit is shown too high.",
                $"যাচাই করুন: {N(noFeed.Count)}টি পশুর খাদ্য খরচ নথিভুক্ত নেই ({TagList(noFeed)}), তাই তাদের লাভ বেশি দেখাচ্ছে।"));
        }

        var noPrice = rows.Where(r => r.NoPriceRecorded).Select(r => r.TagId).ToList();
        if (noPrice.Count > 0)
        {
            insights.Add(T(
                $"Check: {N(noPrice.Count)} sale(s) have no price recorded ({TagList(noPrice)}) and show as a full loss.",
                $"যাচাই করুন: {N(noPrice.Count)}টি বিক্রয়ে দাম লেখা নেই ({TagList(noPrice)}), তাই পুরো ক্ষতি দেখাচ্ছে।"));
        }

        var noWeight = priced.Count - weighed.Count;
        if (noWeight > 0 && weighed.Count > 0)
        {
            insights.Add(T(
                $"{N(noWeight)} sale(s) have no sale weight, so they are left out of the per-kg figures.",
                $"{N(noWeight)}টি বিক্রয়ে ওজন লেখা নেই, তাই প্রতি কেজির হিসাবে ধরা হয়নি।"));
        }

        return new ReportSummary(
            T("Summary", "সারসংক্ষেপ"),
            figures,
            T("What it tells us", "যা বোঝা যাচ্ছে"),
            insights);

        string MarginText(SoldAnimalProfitReportRow r) =>
            r.Margin is { } m ? T($" ({P(m)} margin)", $" ({P(m)} মার্জিন)") : string.Empty;
    }

    private static string StripCheck(string verdict)
    {
        var cut = verdict.IndexOf(" · ", StringComparison.Ordinal);
        var text = cut < 0 ? verdict : verdict[..cut];
        return text.StartsWith("Loss — ", StringComparison.Ordinal) ? text["Loss — ".Length..]
            : text.StartsWith("ক্ষতি — ", StringComparison.Ordinal) ? text["ক্ষতি — ".Length..]
            : text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
    }

    private static string TagList(List<string> tags) =>
        tags.Count <= 6 ? string.Join(", ", tags) : string.Join(", ", tags.Take(6)) + $" +{tags.Count - 6}";

    private Task<string?> BatchNameAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("sold-profit-batch", async () =>
            context.IdOrNull("batchId") is { } id
                ? (await _batches.GetByIdAsync(id, cancellationToken).ConfigureAwait(false))?.Name
                : null);
}
