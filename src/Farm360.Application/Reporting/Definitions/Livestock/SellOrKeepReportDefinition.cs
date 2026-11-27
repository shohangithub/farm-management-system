using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Pricing;
using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Livestock;

/// <summary>
/// One line of the Sell or Keep report. In herd mode a line is an animal; in single-animal mode it is
/// a projection step, a weighing, a month of costs or a treatment — the columns are shared, so their
/// headings are kept general ("Weight", "Cost", "Profit").
/// </summary>
public sealed record SellOrKeepReportRow(
    string GroupKey,
    string GroupLabel,
    string Item,
    DateOnly? Date,
    int? Days,
    decimal? WeightKg,
    decimal? AdgKg,
    string Growth,
    decimal? CostBdt,
    decimal? ValueBdt,
    decimal? ProfitBdt,
    DateOnly? SellAround,
    int? DaysLeft,
    decimal? BestProfitBdt,
    string Advice,
    string Confidence);

/// <summary>
/// Report LV-SK — "Sell or Keep?": for every animal on the farm, what it has cost, what it is worth
/// today, how fast it is growing against its breed, and the most profitable day to sell it, with one
/// plain verdict (sell now / keep about N days / losing money / weigh it first). With an animal
/// chosen it shows that animal's day-by-day projection and its history (weighings, monthly costs,
/// treatments).
/// </summary>
/// <remarks>
/// All the maths is <see cref="SellOrKeepCalculator"/> through <see cref="IAnimalSellOrKeepAdvisor"/> — the
/// same service the animal page card and the livestock list use — so the report and the screen always agree.
/// </remarks>
public sealed class SellOrKeepReportDefinition : ReportDefinition<SellOrKeepReportRow>
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly IAnimalSellOrKeepAdvisor _advisor;
    private readonly IAnimalRepository _animals;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalOverheadAllocationRepository _overheads;
    private readonly IMedicalTreatmentRepository _treatments;

    public SellOrKeepReportDefinition(
        IAnimalSellOrKeepAdvisor advisor,
        IAnimalRepository animals,
        IAnimalFeedAllocationRepository feed,
        IAnimalOverheadAllocationRepository overheads,
        IMedicalTreatmentRepository treatments)
    {
        _advisor = advisor;
        _animals = animals;
        _feed = feed;
        _overheads = overheads;
        _treatments = treatments;
    }

    public override string Key => "livestock.sell-or-keep";

    public override LocalizedText Title => new("Sell or Keep? Animal Decision Report", "রাখবেন না বিক্রি করবেন? পশুভিত্তিক সিদ্ধান্ত");

    public override LocalizedText Description => new(
        "For every animal on the farm: what it has cost, what it is worth today, how fast it grows against its breed, and the most profitable day to sell — with a plain verdict. Choose one animal to see its day-by-day projection and history.",
        "খামারের প্রতিটি পশুর জন্য: এ পর্যন্ত খরচ, আজকের দাম, জাতের তুলনায় বৃদ্ধি এবং বিক্রির সবচেয়ে লাভজনক দিন — সাথে সহজ পরামর্শ। একটি পশু বেছে নিলে দিনভিত্তিক হিসাব ও ইতিহাস দেখাবে।");

    public override ReportCategory Category => ReportCategory.Livestock;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<LocalizedText> Notes
    {
        get
        {
            var a = _advisor.Assumptions;
            var decline = (a.GrowthDeclineFraction * 100m).ToString("0", Invariant);
            return
            [
                new("Invested = purchase + feed + vet & medicine + labour and overhead share so far (the animal's cost book). Profit = weight × price per kg (+ by-products per head, if any) − invested.",
                    "বিনিয়োগ = ক্রয়মূল্য + খাদ্য + চিকিৎসা ও ওষুধ + এ পর্যন্ত শ্রম ও পরোক্ষ খরচের অংশ (পশুর খরচের হিসাব)। লাভ = ওজন × প্রতি কেজি দাম (+ প্রতি পশুর উপজাত, যদি থাকে) − বিনিয়োগ।"),
                new($"Growth = the straight-line trend of the weighings in the last {a.RecentGrowthWindowDays} days (at least 2); otherwise the gain from the first to the last weighing. Today's weight adds the growth since the last weighing.",
                    $"বৃদ্ধি = গত {a.RecentGrowthWindowDays} দিনের ওজনগুলোর সরলরেখা প্রবণতা (অন্তত ২টি); না থাকলে প্রথম থেকে শেষ ওজনের বৃদ্ধি। আজকের ওজনে শেষ ওজনের পরের বৃদ্ধি যোগ করা হয়েছে।"),
                new($"Growth slows as the animal gets heavier: the projected gain per day falls by {decline}% over {a.GrowthDeclineDays} days and then stays there. It is compared with the breed's standard gain (±{(a.AtTargetBand * 100m).ToString("0", Invariant)}% = at target).",
                    $"ওজন বাড়লে বৃদ্ধি কমে: অনুমিত দৈনিক বৃদ্ধি {a.GrowthDeclineDays} দিনে {decline}% কমে, তারপর সেখানেই থাকে। জাতের মান বৃদ্ধির সাথে তুলনা করা হয়েছে (±{(a.AtTargetBand * 100m).ToString("0", Invariant)}% = লক্ষ্যে)।"),
                new($"Daily cost = feed eaten in the last {a.FeedWindowDays} days ÷ days + labour & overhead and health (treatments, vaccines) per day over the last {a.HealthWindowDays} days. Feed grows with body size (weight^0.75).",
                    $"দৈনিক খরচ = গত {a.FeedWindowDays} দিনের খাদ্য ÷ দিন + গত {a.HealthWindowDays} দিনের দৈনিক শ্রম ও পরোক্ষ এবং স্বাস্থ্য (চিকিৎসা, টিকা) খরচ। শরীর বড় হলে খাদ্য বাড়ে (ওজন^০.৭৫)।"),
                new($"Price per kg live weight — the same price the animal page uses: your own sales of the same breed in the last {AnimalPriceResolution.OwnSalesLookbackDays / 30} months (at least {AnimalPriceResolution.MinOwnSales}), else the breed's price, else the farm rule: the live-weight price, or meat price × dressing % × (1 + by-products %) plus by-products per head. A farm that values at cost uses the meat estimate here.",
                    $"প্রতি কেজি জীবিত ওজনের দাম — পশুর পাতায় যে দাম, সেটিই: গত {AnimalPriceResolution.OwnSalesLookbackDays / 30} মাসে একই জাতের নিজের বিক্রি (অন্তত {AnimalPriceResolution.MinOwnSales}টি), না হলে জাতের দাম, না হলে খামারের নিয়ম: জীবিত ওজনের দাম, অথবা মাংসের দাম × ড্রেসিং % × (১ + উপজাত %) এবং প্রতি পশুর উপজাতের দাম। খরচে মূল্যায়ন করা খামারে এখানে মাংসের অনুমান ব্যবহার হয়।"),
                new($"Best day = the day profit is highest in the next {a.HorizonDays} days: keep while one more day's gain is worth more than that day's cost. Sell now when it is within {a.SellNowWithinDays} days, or when an often-sick animal is in profit.",
                    $"সেরা দিন = আগামী {a.HorizonDays} দিনে যেদিন লাভ সবচেয়ে বেশি: যতদিন একদিনের বৃদ্ধির দাম সেদিনের খরচের চেয়ে বেশি, ততদিন রাখুন। {a.SellNowWithinDays} দিনের মধ্যে হলে, বা প্রায়ই অসুস্থ পশু লাভে থাকলে এখনই বিক্রি।"),
                new($"Often sick = {a.FrequentlySickTreatments} or more treatments in {a.HealthWindowDays} days, or health cost above {(a.FrequentlySickHealthShareOfFeed * 100m).ToString("0", Invariant)}% of feed cost. Confidence is lower with few or scattered weighings, sickness, or a weighing older than {a.StaleWeighingDays} days.",
                    $"প্রায়ই অসুস্থ = {a.HealthWindowDays} দিনে {a.FrequentlySickTreatments}টি বা বেশি চিকিৎসা, বা খাদ্য খরচের {(a.FrequentlySickHealthShareOfFeed * 100m).ToString("0", Invariant)}%-এর বেশি স্বাস্থ্য খরচ। ওজন কম বা এলোমেলো হলে, অসুস্থতায়, বা শেষ ওজন {a.StaleWeighingDays} দিনের বেশি পুরনো হলে নির্ভরযোগ্যতা কম।"),
                new("A projection, not a promise: market prices change. Weigh animals every 2–4 weeks to keep the advice accurate.",
                    "এটি অনুমান, নিশ্চয়তা নয়: বাজারদর বদলায়। পরামর্শ সঠিক রাখতে প্রতি ২–৪ সপ্তাহে ওজন নিন।"),
            ];
        }
    }

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Batch(name: "batchId", required: false),
        ReportParameter.Animal("animalId", required: false),
    ];

    public override IReadOnlyList<ReportColumn<SellOrKeepReportRow>> Columns =>
    [
        ReportColumn<SellOrKeepReportRow>.Text("item", new LocalizedText("Animal / line", "পশু / বিষয়"), r => r.Item, width: 1.7f),
        ReportColumn<SellOrKeepReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.WholeNumber("days", new LocalizedText("Days", "দিন"), r => r.Days, widthMm: 11f),
        ReportColumn<SellOrKeepReportRow>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, 1, 15f),
        ReportColumn<SellOrKeepReportRow>.Number("adg", new LocalizedText("Gain/day (kg)", "দৈনিক বৃদ্ধি (কেজি)"), r => r.AdgKg, 2, 14f),
        ReportColumn<SellOrKeepReportRow>.Text("growth", new LocalizedText("vs breed", "জাতের তুলনায়"), r => r.Growth, width: 0.8f),
        ReportColumn<SellOrKeepReportRow>.Money("cost", new LocalizedText("Invested / cost (BDT)", "বিনিয়োগ / খরচ (টাকা)"), r => r.CostBdt, decimals: 0, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.Money("value", new LocalizedText("Value (BDT)", "মূল্য (টাকা)"), r => r.ValueBdt, decimals: 0, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.Money("profit", new LocalizedText("Profit / (loss) (BDT)", "লাভ / (ক্ষতি) (টাকা)"), r => r.ProfitBdt, decimals: 0, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.Date("sellAround", new LocalizedText("Sell around", "বিক্রির সময়"), r => r.SellAround, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.WholeNumber("daysLeft", new LocalizedText("Days left", "বাকি দিন"), r => r.DaysLeft, widthMm: 12f),
        ReportColumn<SellOrKeepReportRow>.Money("bestProfit", new LocalizedText("Profit at best day (BDT)", "সেরা দিনে লাভ (টাকা)"), r => r.BestProfitBdt, decimals: 0, widthMm: 19f),
        ReportColumn<SellOrKeepReportRow>.Text("advice", new LocalizedText("Advice", "পরামর্শ"), r => r.Advice, width: 1.9f),
        ReportColumn<SellOrKeepReportRow>.Text("confidence", new LocalizedText("Confidence", "নির্ভরযোগ্যতা"), r => r.Confidence, width: 0.7f),
    ];

    public override ReportGroup<SellOrKeepReportRow>? Group =>
        ReportGroup<SellOrKeepReportRow>.By(r => r.GroupKey, r => r.GroupLabel);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var bn = context.Language == ReportLanguage.Bn;
        if (data.Single is { } one)
            return $"{(bn ? "ট্যাগ " : "Tag ")}{one.TagId} · {one.BreedName}" + (one.BatchName is null ? string.Empty : $" · {(bn ? "ব্যাচ " : "Batch ")}{one.BatchName}");

        var batch = data.Advice.Select(x => x.BatchName).FirstOrDefault(n => n is not null);
        var parts = new List<string> { bn ? $"খামারের পশু, {AnimalCostReportText.Day(context.Today)}" : $"Animals on the farm, {AnimalCostReportText.Day(context.Today)}" };
        if (context.IdOrNull("batchId") is not null && batch is not null)
            parts.Add((bn ? "ব্যাচ " : "Batch ") + batch);
        return string.Join(" · ", parts);
    }

    protected override async Task<IReadOnlyList<SellOrKeepReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Rows;
    }

    protected override async Task<ReportSummary?> SummarizeAsync(ReportContext context, IReadOnlyList<SellOrKeepReportRow> rows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var data = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        return data.Single is { } one
            ? SummarizeAnimal(context, one, _advisor.Assumptions)
            : SummarizeHerd(context, data.Advice, _advisor.Assumptions);
    }

    // ── Loading ─────────────────────────────────────────────────────────────

    private sealed record Loaded(IReadOnlyList<SellOrKeepAdvice> Advice, SellOrKeepAdvice? Single, IReadOnlyList<SellOrKeepReportRow> Rows);

    private Task<Loaded> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("sell-or-keep", async () =>
        {
            var farmId = context.FarmId();
            var animalId = context.IdOrNull("animalId");
            var advice = await _advisor.AdviseFarmAsync(
                farmId, context.Today, context.IdOrNull("batchId"), animalId, includeProjection: animalId is not null, cancellationToken).ConfigureAwait(false);

            if (animalId is not null)
            {
                var one = advice.Count > 0 ? advice[0] : null;
                if (one is null)
                    return new Loaded(advice, null, []);
                var rows = await AnimalRowsAsync(context, farmId, one, cancellationToken).ConfigureAwait(false);
                return new Loaded(advice, one, rows);
            }

            return new Loaded(advice, null, HerdRows(advice, context.Language));
        });

    /// <summary>Herd mode: one row per animal, grouped by verdict with "Sell now" first.</summary>
    public static IReadOnlyList<SellOrKeepReportRow> HerdRows(IReadOnlyList<SellOrKeepAdvice> advice, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(advice);
        var bn = language == ReportLanguage.Bn;
        return advice
            .OrderBy(x => GroupOrder(x.Verdict))
            .ThenBy(x => x.Verdict == SellOrKeepVerdict.Keep ? x.BestDay ?? 0 : 0)
            .ThenByDescending(x => x.Verdict == SellOrKeepVerdict.LosingMoney ? -(x.ProfitTodayBdt ?? 0m) : x.ProfitTodayBdt ?? 0m)
            .ThenBy(x => x.TagId, StringComparer.OrdinalIgnoreCase)
            .Select(x =>
            {
                var (key, label) = VerdictGroup(x.Verdict, language);
                var advice1 = VerdictShort(x, language);
                if (x.FrequentlySick)
                    advice1 += bn ? " · প্রায়ই অসুস্থ" : " · often sick";
                return new SellOrKeepReportRow(
                    key, label,
                    $"{x.TagId} · {x.BreedName}",
                    x.LatestWeightDate,
                    x.DaysOnFarm,
                    x.WeightTodayKg,
                    x.AdgKgPerDay,
                    GrowthText(x, language),
                    x.InvestedBdt,
                    x.ValueTodayBdt,
                    x.ProfitTodayBdt,
                    x.Verdict is SellOrKeepVerdict.Keep or SellOrKeepVerdict.SellNow ? x.BestDate : null,
                    x.Verdict == SellOrKeepVerdict.Keep ? x.BestDay : null,
                    x.ProfitAtBestDayBdt,
                    advice1,
                    ConfidenceText(x.Confidence, language));
            })
            .ToList();
    }

    /// <summary>Single-animal mode: the projection every N days, then weighings, monthly costs and treatments.</summary>
    private async Task<IReadOnlyList<SellOrKeepReportRow>> AnimalRowsAsync(ReportContext context, Guid farmId, SellOrKeepAdvice x, CancellationToken cancellationToken)
    {
        var language = context.Language;
        var bn = language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;
        string Tk(decimal v) => v.ToString("#,##0", Invariant) + T(" BDT", " টাকা");
        var rows = new List<SellOrKeepReportRow>();
        var confidence = ConfidenceText(x.Confidence, language);

        // ── 1. Projection ──
        var p1 = ("1", T($"1. Projection (every {_advisor.Assumptions.ProjectionTableStepDays} days)", $"১. ভবিষ্যৎ হিসাব (প্রতি {_advisor.Assumptions.ProjectionTableStepDays} দিন)"));
        var step = Math.Max(1, _advisor.Assumptions.ProjectionTableStepDays);
        foreach (var p in x.Projection.Where(p => p.Day % step == 0 || p.Day == x.BestDay || p.Day == x.BreakEvenDay))
        {
            var marks = new List<string>();
            if (p.Day == 0) marks.Add(T("today", "আজ"));
            if (p.Day == x.BestDay) marks.Add(T("best day to sell", "বিক্রির সেরা দিন"));
            if (p.Day == x.BreakEvenDay) marks.Add(T("break-even", "খরচ উঠে আসে"));
            var item = p.Day == 0 ? T("Today", "আজ") : T($"Day +{p.Day}", $"দিন +{p.Day}");
            rows.Add(new SellOrKeepReportRow(p1.Item1, p1.Item2, item, p.Date, p.Day, p.WeightKg, p.AdgKg, string.Empty,
                p.CostBdt, p.ValueBdt, p.ProfitBdt, null, null, null,
                p.Day == 0 ? string.Join(", ", marks) : string.Join(", ", marks.Prepend(T($"cost {Tk(p.DailyCostBdt)}/day", $"দৈনিক খরচ {Tk(p.DailyCostBdt)}"))),
                confidence));
        }

        // ── 2. Weighings ──
        var p2 = ("2", T("2. Weighings", "২. ওজনের ইতিহাস"));
        var weighIns = (await _animals.GetWeighInsByFarmAsync(farmId, context.Today.AddDays(-(x.DaysOnFarm + 1)), context.Today, cancellationToken).ConfigureAwait(false))
            .Where(w => w.AnimalId == x.AnimalId)
            .OrderBy(w => w.Date)
            .ToList();
        for (var i = 0; i < weighIns.Count; i++)
        {
            var w = weighIns[i];
            int? gap = null;
            decimal? adg = null;
            if (i > 0 && w.Date > weighIns[i - 1].Date)
            {
                gap = w.Date.DayNumber - weighIns[i - 1].Date.DayNumber;
                adg = Math.Round((w.WeightKg - weighIns[i - 1].WeightKg) / gap.Value, 3);
            }

            rows.Add(new SellOrKeepReportRow(p2.Item1, p2.Item2, i == 0 ? T("First weighing", "প্রথম ওজন") : T("Weighed", "ওজন নেওয়া"),
                w.Date, gap, w.WeightKg, adg, string.Empty, null, null, null, null, null, null,
                gap is null ? string.Empty : T($"{gap} days since the last weighing", $"আগের ওজনের {gap} দিন পর"), string.Empty));
        }

        // ── 3. Monthly costs ──
        var p3 = ("3", T("3. Costs month by month", "৩. মাসভিত্তিক খরচ"));
        var from = context.Today.AddDays(-x.DaysOnFarm);
        var months = new SortedDictionary<DateOnly, (decimal Feed, decimal Labour, decimal Health)>();
        static DateOnly Month(DateOnly d) => new(d.Year, d.Month, 1);
        foreach (var f in (await _feed.GetDailyEatenByFarmAsync(farmId, from, context.Today, cancellationToken).ConfigureAwait(false)).Where(f => f.AnimalId == x.AnimalId))
        {
            var m = months.GetValueOrDefault(Month(f.Date));
            months[Month(f.Date)] = (m.Feed + f.CostBdt, m.Labour, m.Health);
        }

        foreach (var s in await _overheads.GetByAnimalAsync(x.AnimalId, from, context.Today, cancellationToken).ConfigureAwait(false))
        {
            var m = months.GetValueOrDefault(Month(s.PeriodStart));
            months[Month(s.PeriodStart)] = (m.Feed, m.Labour + s.AllocatedAmountBdt, m.Health);
        }

        var treatments = (await _treatments.GetByAnimalIdAsync(x.AnimalId, cancellationToken).ConfigureAwait(false))
            .OrderBy(t => t.StartDate)
            .ToList();
        foreach (var t in treatments)
        {
            var m = months.GetValueOrDefault(Month(t.StartDate));
            months[Month(t.StartDate)] = (m.Feed, m.Labour, m.Health + t.CostBdt);
        }

        foreach (var (month, m) in months)
        {
            var total = m.Feed + m.Labour + m.Health;
            if (total == 0m)
                continue;
            rows.Add(new SellOrKeepReportRow(p3.Item1, p3.Item2, AnimalCostReportText.MonthLabel(month).For(language), month, null, null, null, string.Empty,
                Math.Round(total, 0), null, null, null, null, null,
                T($"feed {Tk(m.Feed)} · labour & overhead {Tk(m.Labour)} · health {Tk(m.Health)}",
                    $"খাদ্য {Tk(m.Feed)} · শ্রম ও পরোক্ষ {Tk(m.Labour)} · স্বাস্থ্য {Tk(m.Health)}"),
                string.Empty));
        }

        // ── 4. Illnesses and treatments ──
        var p4 = ("4", T("4. Illnesses & treatments", "৪. অসুখ ও চিকিৎসা"));
        foreach (var t in treatments)
        {
            var what = string.IsNullOrWhiteSpace(t.Diagnosis) ? "—" : t.Diagnosis.Trim();
            var details = string.IsNullOrWhiteSpace(t.MedicationName) ? string.Empty : t.MedicationName.Trim();
            rows.Add(new SellOrKeepReportRow(p4.Item1, p4.Item2, T("Treatment: ", "চিকিৎসা: ") + what, t.StartDate,
                t.EndDate is { } end ? end.DayNumber - t.StartDate.DayNumber + 1 : null, null, null, string.Empty,
                t.CostBdt > 0m ? t.CostBdt : null, null, null, null, null, null, details, string.Empty));
        }

        return rows;
    }

    // ── Labels ──────────────────────────────────────────────────────────────

    private static int GroupOrder(SellOrKeepVerdict v) => v switch
    {
        SellOrKeepVerdict.SellNow => 1,
        SellOrKeepVerdict.Keep => 2,
        SellOrKeepVerdict.LosingMoney => 3,
        _ => 4,
    };

    /// <summary>The group band for a verdict ("1. Sell now" first).</summary>
    public static (string Key, string Label) VerdictGroup(SellOrKeepVerdict v, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return v switch
        {
            SellOrKeepVerdict.SellNow => ("1", bn ? "১. এখনই বিক্রি করুন" : "1. Sell now"),
            SellOrKeepVerdict.Keep => ("2", bn ? "২. আরও কিছুদিন রাখুন" : "2. Keep for now"),
            SellOrKeepVerdict.LosingMoney => ("3", bn ? "৩. লোকসান হচ্ছে — খাদ্য বা স্বাস্থ্য দেখুন" : "3. Losing money — check feed or health"),
            _ => ("4", bn ? "৪. আগে ওজন নিন" : "4. Weigh it first"),
        };
    }

    /// <summary>The verdict in a few words for a table cell.</summary>
    public static string VerdictShort(SellOrKeepAdvice x, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(x);
        var bn = language == ReportLanguage.Bn;
        return x.Verdict switch
        {
            SellOrKeepVerdict.SellNow => bn ? "এখনই বিক্রি করুন" : "Sell now",
            SellOrKeepVerdict.Keep => bn ? $"আরও ~{x.BestDay} দিন রাখুন" : $"Keep ~{x.BestDay} more days",
            SellOrKeepVerdict.LosingMoney => (bn ? "লোকসান — " : "Losing money — ") + CauseShort(x.LossCause, language),
            _ => bn ? "আগে ওজন নিন" : "Weigh it first",
        };
    }

    /// <summary>The likely cause of a loss in two or three words.</summary>
    public static string CauseShort(SellOrKeepLossCause cause, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return cause switch
        {
            SellOrKeepLossCause.Sickness => bn ? "অসুস্থতা" : "sickness",
            SellOrKeepLossCause.SlowGrowth => bn ? "ধীর বৃদ্ধি" : "slow growth",
            SellOrKeepLossCause.FeedCost => bn ? "খাদ্য খরচ বেশি" : "feed cost too high",
            SellOrKeepLossCause.PurchasePrice => bn ? "কেনা দাম বেশি" : "bought too dear",
            _ => string.Empty,
        };
    }

    public static string ConfidenceText(SellOrKeepConfidence c, ReportLanguage language)
    {
        var bn = language == ReportLanguage.Bn;
        return c switch
        {
            SellOrKeepConfidence.High => bn ? "বেশি" : "High",
            SellOrKeepConfidence.Medium => bn ? "মাঝারি" : "Medium",
            _ => bn ? "কম" : "Low",
        };
    }

    /// <summary>"below 0.70" / "at 0.70" / "above 0.70" — against the breed's target gain per day.</summary>
    public static string GrowthText(SellOrKeepAdvice x, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(x);
        var bn = language == ReportLanguage.Bn;
        var t = x.BreedTargetAdgKg?.ToString("0.00", Invariant) ?? string.Empty;
        return x.GrowthVsTarget switch
        {
            SellOrKeepGrowthVsTarget.Below => bn ? $"কম ({t})" : $"below {t}",
            SellOrKeepGrowthVsTarget.At => bn ? $"সমান ({t})" : $"at {t}",
            SellOrKeepGrowthVsTarget.Above => bn ? $"বেশি ({t})" : $"above {t}",
            _ => "—",
        };
    }

    /// <summary>Where the price per kg came from, in plain words.</summary>
    public static string PriceSourceText(SellOrKeepAdvice x, ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(x);
        var bn = language == ReportLanguage.Bn;
        return bn ? x.Price.ExplanationBn : x.Price.ExplanationEn;
    }

    // ── Summary boxes ───────────────────────────────────────────────────────

    /// <summary>Herd mode: how many to sell now, extra profit by following the advice, best and worst, losers and why.</summary>
    public static ReportSummary SummarizeHerd(ReportContext context, IReadOnlyList<SellOrKeepAdvice> advice, SellOrKeepAssumptions? assumptions = null)
    {
        var horizon = (assumptions ?? SellOrKeepAssumptions.Default).HorizonDays;
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(advice);

        var language = context.Language;
        var bn = language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 0, context.BengaliNumerals, language);
        string N(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals, language);
        string Bdt(decimal v) => M(v) + T(" BDT", " টাকা");
        string Tone(decimal v) => v > 0m ? ReportTone.Good : v < 0m ? ReportTone.Bad : ReportTone.Neutral;
        string Tags(IEnumerable<SellOrKeepAdvice> xs, int max = 8)
        {
            var list = xs.Select(x => x.TagId).ToList();
            return string.Join(", ", list.Take(max)) + (list.Count > max ? $" +{N(list.Count - max)}" : string.Empty);
        }

        var sellNow = advice.Where(x => x.Verdict == SellOrKeepVerdict.SellNow).ToList();
        var keep = advice.Where(x => x.Verdict == SellOrKeepVerdict.Keep).ToList();
        var losing = advice.Where(x => x.Verdict == SellOrKeepVerdict.LosingMoney).ToList();
        var weigh = advice.Where(x => x.Verdict == SellOrKeepVerdict.WeighFirst).ToList();
        var priced = advice.Where(x => x.ProfitTodayBdt is not null).ToList();
        var invested = advice.Sum(x => x.InvestedBdt);
        var valueToday = priced.Sum(x => x.ValueTodayBdt ?? 0m);
        var profitToday = priced.Sum(x => x.ProfitTodayBdt ?? 0m);
        var extra = keep.Sum(x => x.ExtraProfitBdt ?? 0m);
        var sick = advice.Where(x => x.FrequentlySick).ToList();

        var figures = new List<ReportSummaryFigure>
        {
            new(T("Animals on the farm", "খামারের পশু"), N(advice.Count)),
            new(T("Sell now", "এখনই বিক্রি"), N(sellNow.Count), sellNow.Count > 0 ? ReportTone.Good : ReportTone.Neutral),
            new(T("Keep for now", "আরও রাখুন"), N(keep.Count)),
            new(T("Losing money", "লোকসানে"), N(losing.Count), losing.Count > 0 ? ReportTone.Bad : ReportTone.Neutral),
            new(T("Weigh first", "আগে ওজন নিন"), N(weigh.Count)),
            new(T("Total invested", "মোট বিনিয়োগ"), Bdt(invested)),
            new(T("Value today", "আজকের মূল্য"), Bdt(valueToday)),
            new(profitToday >= 0m ? T("Profit if all sold today", "আজ সব বিক্রি করলে লাভ") : T("Loss if all sold today", "আজ সব বিক্রি করলে ক্ষতি"), Bdt(profitToday), Tone(profitToday)),
            new(T("Extra profit by following the advice", "পরামর্শ মানলে বাড়তি লাভ"), Bdt(extra), Tone(extra)),
        };

        var insights = new List<string>();
        if (sellNow.Count > 0)
        {
            var p = sellNow.Sum(x => x.ProfitTodayBdt ?? 0m);
            insights.Add(T($"Sell now: {N(sellNow.Count)} animal(s) — {Tags(sellNow)}. Together they make {Bdt(p)} if sold today; keeping them longer adds more cost than weight.",
                $"এখনই বিক্রি: {N(sellNow.Count)}টি পশু — {Tags(sellNow)}। আজ বিক্রি করলে মোট {Bdt(p)}; বেশি দিন রাখলে ওজনের চেয়ে খরচ বেশি বাড়বে।"));
        }

        if (keep.Count > 0)
        {
            var soonest = keep.OrderBy(x => x.BestDay).First();
            insights.Add(T($"Keep: {N(keep.Count)} animal(s) are still growing profitably. Selling each on its best day adds about {Bdt(extra)} over selling today. First due: {soonest.TagId} in about {N(soonest.BestDay ?? 0)} days.",
                $"রাখুন: {N(keep.Count)}টি পশু এখনও লাভজনকভাবে বাড়ছে। প্রতিটি তার সেরা দিনে বিক্রি করলে আজকের চেয়ে প্রায় {Bdt(extra)} বেশি লাভ। প্রথমটি: {soonest.TagId}, প্রায় {N(soonest.BestDay ?? 0)} দিন পর।"));
        }

        var best = priced.Where(x => x.ProfitAtBestDayBdt is not null).OrderByDescending(x => x.ProfitAtBestDayBdt).Take(3).ToList();
        if (best.Count > 0)
        {
            insights.Add(T("Best performers: " + string.Join("; ", best.Select(x => $"{x.TagId} ({x.BreedName}) up to {Bdt(x.ProfitAtBestDayBdt!.Value)}, gaining {Kg(x.AdgKgPerDay)} kg/day")) + ".",
                "সেরা পশু: " + string.Join("; ", best.Select(x => $"{x.TagId} ({x.BreedName}) সর্বোচ্চ {Bdt(x.ProfitAtBestDayBdt!.Value)}, দৈনিক {Kg(x.AdgKgPerDay)} কেজি বৃদ্ধি")) + "।"));
        }

        var worst = priced.Where(x => x.ProfitAtBestDayBdt is not null).OrderBy(x => x.ProfitAtBestDayBdt).Take(3)
            .Where(x => !best.Contains(x)).ToList();
        if (worst.Count > 0)
        {
            insights.Add(T("Weakest: " + string.Join("; ", worst.Select(x => $"{x.TagId} at best {Bdt(x.ProfitAtBestDayBdt!.Value)}, gaining {Kg(x.AdgKgPerDay)} kg/day ({GrowthText(x, language)})")) + ".",
                "দুর্বল পশু: " + string.Join("; ", worst.Select(x => $"{x.TagId} সর্বোচ্চ {Bdt(x.ProfitAtBestDayBdt!.Value)}, দৈনিক {Kg(x.AdgKgPerDay)} কেজি ({GrowthText(x, language)})")) + "।"));
        }

        if (losing.Count > 0)
        {
            insights.Add(T($"Losing money: {N(losing.Count)} animal(s) never cover their cost in the next {N(horizon)} days — "
                    + string.Join("; ", losing.Take(8).Select(x => $"{x.TagId}: {CauseShort(x.LossCause, language)}")) + ". Check their ration and health, or sell when a good price comes.",
                $"লোকসানে: {N(losing.Count)}টি পশু আগামী {N(horizon)} দিনেও খরচ তুলতে পারবে না — "
                    + string.Join("; ", losing.Take(8).Select(x => $"{x.TagId}: {CauseShort(x.LossCause, language)}")) + "। খাদ্য ও স্বাস্থ্য দেখুন, বা ভালো দাম পেলে বিক্রি করুন।"));
        }

        if (sick.Count > 0)
        {
            insights.Add(T($"Often sick: {Tags(sick)} — their growth is less reliable, so sell them sooner.",
                $"প্রায়ই অসুস্থ: {Tags(sick)} — এদের বৃদ্ধি অনিশ্চিত, তাই আগে বিক্রি করুন।"));
        }

        if (weigh.Count > 0)
        {
            insights.Add(T($"Weigh first: {Tags(weigh)} — at least two weighings are needed before the app can advise.",
                $"আগে ওজন নিন: {Tags(weigh)} — পরামর্শের জন্য অন্তত দুবার ওজন দরকার।"));
        }

        var sources = advice.GroupBy(x => (x.Price.Source, x.Price.ExplanationEn)).OrderBy(g => g.Key.Source)
            .Select(g => $"{N(g.Count())} — {PriceSourceText(g.First(), language)}").ToList();
        if (sources.Count > 0)
            insights.Add(T("Price per kg used: ", "যে দামে হিসাব: ") + string.Join("; ", sources) + T(".", "।"));

        return new ReportSummary(T("Summary", "সারসংক্ষেপ"), figures, T("What to do", "করণীয়"), insights);

        static string Kg(decimal? v) => v?.ToString("0.00", Invariant) ?? "—";
    }

    /// <summary>Single-animal mode: the verdict and every number it rests on.</summary>
    public static ReportSummary SummarizeAnimal(ReportContext context, SellOrKeepAdvice x, SellOrKeepAssumptions assumptions)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(assumptions);

        var language = context.Language;
        var bn = language == ReportLanguage.Bn;
        string T(string en, string b) => bn ? b : en;
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 0, context.BengaliNumerals, language);
        string N(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals, language);
        string K(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Number, 1, context.BengaliNumerals, language) + T(" kg", " কেজি");
        string Bdt(decimal v) => M(v) + T(" BDT", " টাকা");
        string Tone(decimal? v) => v > 0m ? ReportTone.Good : v < 0m ? ReportTone.Bad : ReportTone.Neutral;
        string Day(DateOnly? d) => d is { } v ? AnimalCostReportText.Day(v) : "—";

        var verdictTone = x.Verdict switch
        {
            SellOrKeepVerdict.LosingMoney => ReportTone.Bad,
            SellOrKeepVerdict.SellNow or SellOrKeepVerdict.Keep when x.ProfitAtBestDayBdt > 0m => ReportTone.Good,
            _ => ReportTone.Neutral,
        };

        var figures = new List<ReportSummaryFigure>
        {
            new(T("Advice", "পরামর্শ"), VerdictShort(x, language), verdictTone),
            new(T("Confidence", "নির্ভরযোগ্যতা"), ConfidenceText(x.Confidence, language)),
            new(T("Days on farm", "খামারে দিন"), N(x.DaysOnFarm)),
            new(T("Invested so far", "এ পর্যন্ত বিনিয়োগ"), Bdt(x.InvestedBdt)),
            new(T("Weight today (est.)", "আজকের ওজন (আনুমানিক)"), x.WeightTodayKg is { } w ? K(w) : "—"),
            new(T("Gain per day", "দৈনিক বৃদ্ধি"), x.AdgKgPerDay is { } g ? $"{g.ToString("0.00", Invariant)} {T("kg", "কেজি")} ({GrowthText(x, language)})" : "—"),
            new(T("Price per kg", "প্রতি কেজি দাম"), Bdt(x.Price.PricePerKgBdt)),
            new(T("Value today", "আজকের মূল্য"), x.ValueTodayBdt is { } v ? Bdt(v) : "—"),
            new(x.ProfitTodayBdt < 0m ? T("Loss if sold today", "আজ বিক্রি করলে ক্ষতি") : T("Profit if sold today", "আজ বিক্রি করলে লাভ"), x.ProfitTodayBdt is { } p ? Bdt(p) : "—", Tone(x.ProfitTodayBdt)),
            new(T("Best day to sell", "বিক্রির সেরা দিন"), x.BestDay is { } bd ? $"{Day(x.BestDate)} (+{N(bd)})" : "—"),
            new(T("Profit at best day", "সেরা দিনে লাভ"), x.ProfitAtBestDayBdt is { } pb ? Bdt(pb) : "—", Tone(x.ProfitAtBestDayBdt)),
            new(T("Cost per day", "দৈনিক খরচ"), Bdt(x.DailyCostBdt)),
        };
        if (x.BreakEvenDay is { } be)
            figures.Add(new(T("Break-even", "খরচ উঠবে"), $"{Day(x.BreakEvenDate)} (+{N(be)})"));
        if (x.MarginPerDayKeptBdt is { } mpd)
            figures.Add(new(T("Earned per day kept", "প্রতিদিন রাখলে আয়"), Bdt(mpd), Tone(mpd)));

        var insights = new List<string> { bn ? x.ReasonBn : x.ReasonEn };

        if (x.WeightTodayKg is { } wt && x.LatestWeightKg is { } lw)
        {
            insights.Add(T($"Weighed {K(lw)} on {Day(x.LatestWeightDate)}; with its growth since then it is about {K(wt)} today. {N(x.Weighings)} weighing(s) on record.",
                $"{Day(x.LatestWeightDate)} তারিখে ওজন {K(lw)}; তারপরের বৃদ্ধি ধরে আজ প্রায় {K(wt)}। মোট {N(x.Weighings)}টি ওজন নেওয়া হয়েছে।"));
        }

        if (x.AdgKgPerDay is { } adg)
        {
            var how = x.GrowthSource == SellOrKeepGrowthSource.Recent
                ? T($"the trend of {N(x.GrowthWeighings)} weighings in the last {assumptions.RecentGrowthWindowDays} days", $"গত {assumptions.RecentGrowthWindowDays} দিনের {N(x.GrowthWeighings)}টি ওজনের প্রবণতা")
                : T("first to last weighing (no two recent weighings)", "প্রথম থেকে শেষ ওজন (সাম্প্রতিক দুটি ওজন নেই)");
            var target = x.BreedTargetAdgKg is { } t
                ? T($" The breed standard is {t.ToString("0.00", Invariant)} kg/day — it is {GrowthText(x, language)}.", $" জাতের মান {t.ToString("0.00", Invariant)} কেজি/দিন — এটি {GrowthText(x, language)}।")
                : T(" The breed has no growth target set.", " জাতের বৃদ্ধির লক্ষ্য দেওয়া নেই।");
            insights.Add(T($"It gains {adg.ToString("0.00", Invariant)} kg a day ({how}).", $"দৈনিক {adg.ToString("0.00", Invariant)} কেজি বাড়ছে ({how})।") + target);
        }

        insights.Add(T($"Keeping it costs {Bdt(x.DailyCostBdt)} a day: feed {Bdt(x.FeedPerDayBdt)}, labour & overhead {Bdt(x.LabourOverheadPerDayBdt)}, health {Bdt(x.HealthPerDayBdt)}."
                + (x.ValueOfDailyGainBdt is { } vg ? $" Tomorrow's gain is worth about {Bdt(vg)}." : string.Empty)
                + (x.FeedCostPerKgGainBdt is { } fk ? $" Each kg gained costs {Bdt(fk)} in feed and sells for {Bdt(x.Price.PricePerKgBdt)}." : string.Empty),
            $"রাখতে দৈনিক খরচ {Bdt(x.DailyCostBdt)}: খাদ্য {Bdt(x.FeedPerDayBdt)}, শ্রম ও পরোক্ষ {Bdt(x.LabourOverheadPerDayBdt)}, স্বাস্থ্য {Bdt(x.HealthPerDayBdt)}।"
                + (x.ValueOfDailyGainBdt is { } vg2 ? $" আগামীকালের বৃদ্ধির দাম প্রায় {Bdt(vg2)}।" : string.Empty)
                + (x.FeedCostPerKgGainBdt is { } fk2 ? $" প্রতি কেজি বৃদ্ধিতে খাদ্য খরচ {Bdt(fk2)}, বিক্রি দাম {Bdt(x.Price.PricePerKgBdt)}।" : string.Empty)));

        insights.Add(T($"Price {Bdt(x.Price.PricePerKgBdt)} per kg live weight, {PriceSourceText(x, language)}.",
            $"প্রতি কেজি জীবিত ওজন {Bdt(x.Price.PricePerKgBdt)}, {PriceSourceText(x, language)}।"));

        if (x.TreatmentsLast90Days > 0 || x.HealthCostLast90DaysBdt > 0m)
        {
            insights.Add(T($"Health: {N(x.TreatmentsLast90Days)} treatment(s) in the last {assumptions.HealthWindowDays} days costing {Bdt(x.HealthCostLast90DaysBdt)}."
                    + (x.FrequentlySick ? " It is often sick, so its growth is less certain — sell sooner rather than later." : string.Empty),
                $"স্বাস্থ্য: গত {assumptions.HealthWindowDays} দিনে {N(x.TreatmentsLast90Days)}টি চিকিৎসা, খরচ {Bdt(x.HealthCostLast90DaysBdt)}।"
                    + (x.FrequentlySick ? " প্রায়ই অসুস্থ হয়, তাই বৃদ্ধি অনিশ্চিত — দেরি না করে বিক্রি করুন।" : string.Empty)));
        }

        if (x.BatchDeathsLast90Days > 0)
        {
            insights.Add(T($"{N(x.BatchDeathsLast90Days)} animal(s) of its batch died in the last {assumptions.HealthWindowDays} days — watch its health closely.",
                $"গত {assumptions.HealthWindowDays} দিনে এর ব্যাচের {N(x.BatchDeathsLast90Days)}টি পশু মারা গেছে — স্বাস্থ্যের দিকে নজর রাখুন।"));
        }

        insights.Add(x.Confidence switch
        {
            SellOrKeepConfidence.High => T("Confidence is high: several recent weighings lie close to a straight line.", "নির্ভরযোগ্যতা বেশি: সাম্প্রতিক কয়েকটি ওজন প্রায় সরলরেখায়।"),
            SellOrKeepConfidence.Medium => T("Confidence is medium: weigh it again in 2–4 weeks to firm up the advice.", "নির্ভরযোগ্যতা মাঝারি: পরামর্শ পাকা করতে ২–৪ সপ্তাহ পর আবার ওজন নিন।"),
            _ => T("Confidence is low: too few or scattered weighings, sickness or an old weighing — weigh it again before deciding.", "নির্ভরযোগ্যতা কম: ওজন কম বা এলোমেলো, অসুস্থতা বা পুরনো ওজন — সিদ্ধান্তের আগে আবার ওজন নিন।"),
        });

        return new ReportSummary(T("Sell or keep?", "রাখবেন না বিক্রি করবেন?"), figures, T("Why", "কেন"), insights);
    }
}
