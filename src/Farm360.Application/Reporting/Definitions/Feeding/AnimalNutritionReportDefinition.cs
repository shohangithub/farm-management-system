using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Feeding.Queries.Nutrition;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Feeding;

/// <summary>
/// Report FD7 — Nutrition &amp; Growth: day by day what one animal ate (dry matter, protein, energy,
/// calcium, phosphorus) against what its weight and growth need, with the period's gain, daily gain,
/// feed conversion and advice in the heading.
/// </summary>
public sealed class AnimalNutritionReportDefinition : ReportDefinition<NutritionDayDto>
{
    public override string Key => "feeding.nutrition-growth";

    public override LocalizedText Title => new("Nutrition & Growth Report", "পুষ্টি ও বৃদ্ধি প্রতিবেদন");

    public override LocalizedText Description => new(
        "Is this animal getting enough? Day by day, the protein, energy and dry matter it ate against what an animal of its weight needs, with its weight gain over the period.",
        "পশুটি কি যথেষ্ট খাচ্ছে? প্রতিদিন সে কত প্রোটিন, শক্তি ও শুষ্ক পদার্থ খেয়েছে আর তার ওজন অনুযায়ী কত দরকার, এবং এই সময়ে তার ওজন কত বেড়েছে।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "last-30-days"),
    ];

    // The raw protein and energy "need" columns are left out: "vs need (%)" says the same thing in
    // one number a farmer can act on. Dry matter keeps its need beside it as the intake check.
    public override IReadOnlyList<ReportColumn<NutritionDayDto>> Columns =>
    [
        ReportColumn<NutritionDayDto>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<NutritionDayDto>.Number("weight", new LocalizedText("Weight (kg)", "ওজন (কেজি)"), r => r.WeightKg, decimals: 1, widthMm: 18f),
        ReportColumn<NutritionDayDto>.Number("feed", new LocalizedText("Feed eaten (kg)", "খাওয়া খাদ্য (কেজি)"), r => r.FeedKg, decimals: 2, widthMm: 19f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("dm", new LocalizedText("Dry matter eaten (kg)", "খাওয়া শুষ্ক পদার্থ (কেজি)"), r => r.DryMatterKg, decimals: 2, widthMm: 21f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("dmNeed", new LocalizedText("Dry matter needed (kg)", "প্রয়োজনীয় শুষ্ক পদার্থ (কেজি)"), r => r.DryMatterNeedKg, decimals: 2, widthMm: 21f),
        ReportColumn<NutritionDayDto>.Number("cp", new LocalizedText("Protein eaten (g)", "খাওয়া প্রোটিন (গ্রাম)"), r => r.ProteinG, decimals: 0, widthMm: 19f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("cpPct", new LocalizedText("Protein vs need (%)", "প্রোটিন, চাহিদার তুলনায় (%)"), r => r.ProteinPercentOfNeed, decimals: 0, widthMm: 19f),
        ReportColumn<NutritionDayDto>.Number("me", new LocalizedText("Energy eaten (MJ)", "খাওয়া শক্তি (মেগাজুল)"), r => r.EnergyMj, decimals: 1, widthMm: 19f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("mePct", new LocalizedText("Energy vs need (%)", "শক্তি, চাহিদার তুলনায় (%)"), r => r.EnergyPercentOfNeed, decimals: 0, widthMm: 19f),
        ReportColumn<NutritionDayDto>.Number("ca", new LocalizedText("Calcium (g)", "ক্যালসিয়াম (গ্রাম)"), r => r.CalciumG, decimals: 1, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("p", new LocalizedText("Phosphorus (g)", "ফসফরাস (গ্রাম)"), r => r.PhosphorusG, decimals: 1, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Text("recipes", new LocalizedText("Recipes fed", "খাওয়ানো রেসিপি"), r => string.IsNullOrWhiteSpace(r.Recipes) ? "—" : r.Recipes, width: 2f),
    ];

    public override ReportGroup<NutritionDayDto>? Group =>
        ReportGroup<NutritionDayDto>.ByLocalized(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => FeedReportText.Month(r.Date));

    public override LocalizedText? GrandTotalLabel => new("Total eaten in the period", "পুরো সময়ে মোট খাওয়া");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each row is one day: what the animal ate, and how that compares with what an animal of its weight and growth needs.",
            "প্রতিটি সারি একটি দিন: পশুটি কী খেয়েছে, এবং তার ওজন ও বৃদ্ধি অনুযায়ী চাহিদার সঙ্গে তার তুলনা।"),
        new("Dry matter is the feed with its water taken out; an animal needs roughly 2.5% of its body weight in dry matter each day.",
            "শুষ্ক পদার্থ হলো খাদ্য থেকে পানি বাদ দিলে যা থাকে; একটি পশুর প্রতিদিন মোটামুটি দেহের ওজনের ২.৫% শুষ্ক পদার্থ দরকার।"),
        new("\"Vs need (%)\" is what was eaten as a share of the need: about 90–125% is right, under 90% means the animal is short, far above means over-feeding.",
            "\"চাহিদার তুলনায় (%)\" হলো খাওয়া পরিমাণ চাহিদার কত শতাংশ: প্রায় ৯০–১২৫% ঠিক, ৯০%-এর কম মানে ঘাটতি, অনেক বেশি মানে অতিরিক্ত খাওয়ানো।"),
        new("Weights between two weighings are estimated; weigh the animal at the start and end of the period for a true gain.",
            "দুইবার ওজন মাপার মাঝের দিনগুলোর ওজন আনুমানিক; সঠিক বৃদ্ধি জানতে সময়ের শুরু ও শেষে ওজন মাপুন।"),
        new("Feed per kg gain (FCR) in the heading = dry matter eaten ÷ weight gained; 6–8 kg is good for fattening cattle.",
            "শিরোনামের প্রতি কেজি বৃদ্ধিতে খাদ্য (FCR) = খাওয়া শুষ্ক পদার্থ ÷ বেড়ে যাওয়া ওজন; মোটাতাজাকরণে ৬–৮ কেজি ভালো।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var d = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var s = d.Summary;
        var bn = context.Language == ReportLanguage.Bn;

        var parts = new List<string>
        {
            (bn ? "ট্যাগ " : "Tag ") + d.Tag,
            FeedReportText.Species(d.Species).For(context.Language),
            FeedReportText.Period(new DateRange(d.From, d.To)),
        };

        if (s.GainKg is { } gain)
        {
            var weights = $"{N(s.StartWeightKg, "0.#")} → {N(s.EndWeightKg, "0.#")}";
            parts.Add(bn
                ? $"ওজন {weights} কেজি, বৃদ্ধি {N(gain, "0.#")} কেজি (দৈনিক {N(s.AdgKgPerDay, "0.00")} কেজি)"
                : $"weight {weights} kg, gain {N(gain, "0.#")} kg ({N(s.AdgKgPerDay, "0.00")} kg/day)");
        }
        else
        {
            parts.Add(bn ? "বৃদ্ধি অজানা (শুরু ও শেষে ওজন মাপুন)" : "gain unknown (weigh at start and end)");
        }

        if (s.FeedConversionKgDmPerKgGain is { } fcr)
        {
            parts.Add(bn
                ? $"প্রতি কেজি বৃদ্ধিতে {N(fcr, "0.0")} কেজি শুষ্ক পদার্থ (FCR)"
                : $"{N(fcr, "0.0")} kg dry matter per kg gain (FCR)");
        }

        parts.Add(bn
            ? $"প্রোটিন চাহিদার {N(s.AvgProteinPercentOfNeed, "0")}%, শক্তি চাহিদার {N(s.AvgEnergyPercentOfNeed, "0")}%"
            : $"protein {N(s.AvgProteinPercentOfNeed, "0")}% and energy {N(s.AvgEnergyPercentOfNeed, "0")}% of need");

        // The verdict and advice are produced by the nutrition analysis (English only).
        var advice = s.Advice.Count > 0 ? " " + string.Join(" ", s.Advice) : string.Empty;
        return string.Join(" · ", parts) + $" · {s.Verdict}.{advice}";
    }

    protected override async Task<IReadOnlyList<NutritionDayDto>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Days;
    }

    private static string N(decimal? value, string format) =>
        value is { } v ? v.ToString(format, CultureInfo.InvariantCulture) : "—";

    // The subject line and the body both need the analysis; compute it once per run.
    private static Task<AnimalNutritionDto> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("nutrition", () =>
        {
            var range = context.Range("period");
            return context.Send(new GetAnimalNutritionQuery(context.Id("animalId"), range.From, range.To), cancellationToken);
        });
}
