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
/// fibre, calcium, phosphorus) against what its weight and growth need, with the period's gain,
/// ADG, feed conversion and advice in the heading.
/// </summary>
public sealed class AnimalNutritionReportDefinition : ReportDefinition<NutritionDayDto>
{
    public override string Key => "feeding.nutrition-growth";

    public override LocalizedText Title => new("Nutrition & Growth Report", "পুষ্টি ও বৃদ্ধি প্রতিবেদন");

    public override LocalizedText Description => new(
        "Protein, energy, dry matter and minerals one animal received each day against its weight-based needs, with weight gain, ADG and feed conversion.",
        "একটি পশু প্রতিদিন কত প্রোটিন, শক্তি, শুষ্ক পদার্থ ও খনিজ পেয়েছে, ওজন অনুযায়ী তার চাহিদা, এবং ওজন বৃদ্ধি, দৈনিক বৃদ্ধি ও খাদ্য রূপান্তর।");

    public override ReportCategory Category => ReportCategory.Feeding;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal(),
        ReportParameter.DateRange(@default: "last-30-days"),
    ];

    public override IReadOnlyList<ReportColumn<NutritionDayDto>> Columns =>
    [
        ReportColumn<NutritionDayDto>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<NutritionDayDto>.Number("weight", new LocalizedText("Weight kg", "ওজন কেজি"), r => r.WeightKg, decimals: 1, widthMm: 18f),
        ReportColumn<NutritionDayDto>.Number("feed", new LocalizedText("Feed kg", "খাদ্য কেজি"), r => r.FeedKg, decimals: 2, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("dm", new LocalizedText("Dry matter kg", "শুষ্ক পদার্থ"), r => r.DryMatterKg, decimals: 2, widthMm: 19f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("dmNeed", new LocalizedText("DM need kg", "চাহিদা"), r => r.DryMatterNeedKg, decimals: 2, widthMm: 18f),
        ReportColumn<NutritionDayDto>.Number("cp", new LocalizedText("Protein g", "প্রোটিন গ্রাম"), r => r.ProteinG, decimals: 0, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("cpNeed", new LocalizedText("Protein need g", "প্রোটিন চাহিদা"), r => r.ProteinNeedG, decimals: 0, widthMm: 19f),
        ReportColumn<NutritionDayDto>.Number("cpPct", new LocalizedText("Protein % of need", "প্রোটিন %"), r => r.ProteinPercentOfNeed, decimals: 0, widthMm: 17f),
        ReportColumn<NutritionDayDto>.Number("me", new LocalizedText("Energy MJ", "শক্তি MJ"), r => r.EnergyMj, decimals: 1, widthMm: 17f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("meNeed", new LocalizedText("Energy need MJ", "শক্তি চাহিদা"), r => r.EnergyNeedMj, decimals: 1, widthMm: 19f),
        ReportColumn<NutritionDayDto>.Number("mePct", new LocalizedText("Energy % of need", "শক্তি %"), r => r.EnergyPercentOfNeed, decimals: 0, widthMm: 17f),
        ReportColumn<NutritionDayDto>.Number("ca", new LocalizedText("Ca g", "ক্যালসিয়াম"), r => r.CalciumG, decimals: 1, widthMm: 14f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Number("p", new LocalizedText("P g", "ফসফরাস"), r => r.PhosphorusG, decimals: 1, widthMm: 14f, aggregate: ReportAggregate.Sum),
        ReportColumn<NutritionDayDto>.Text("recipes", new LocalizedText("Recipes", "রেসিপি"), r => r.Recipes, width: 2f),
    ];

    public override ReportGroup<NutritionDayDto>? Group =>
        ReportGroup<NutritionDayDto>.By(
            r => r.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            r => r.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture));

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var d = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var s = d.Summary;
        var growth = s.GainKg is { } g
            ? $"{s.StartWeightKg:0.#} → {s.EndWeightKg:0.#} kg (gain {g:0.#} kg, ADG {s.AdgKgPerDay:0.000} kg/day)"
            : "growth unknown (weigh at start and end)";
        var fcr = s.FeedConversionKgDmPerKgGain is { } f ? $", FCR {f:0.0} kg DM/kg gain" : string.Empty;
        return $"Tag {d.Tag} · {d.From:dd-MMM-yyyy} to {d.To:dd-MMM-yyyy} · {growth}{fcr} · "
             + $"daily: {s.AvgDryMatterKgPerDay:0.00} kg DM, {s.AvgProteinGPerDay:0} g protein, {s.AvgEnergyMjPerDay:0.0} MJ · "
             + $"diet {s.DietProteinPercent:0.0}% CP, {s.DietEnergyMjPerKgDm:0.0} MJ/kg DM · "
             + $"protein {s.AvgProteinPercentOfNeed?.ToString("0", CultureInfo.InvariantCulture) ?? "—"}% / energy {s.AvgEnergyPercentOfNeed?.ToString("0", CultureInfo.InvariantCulture) ?? "—"}% of need · "
             + $"{s.Verdict}. {string.Join(" ", s.Advice)}";
    }

    protected override async Task<IReadOnlyList<NutritionDayDto>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return (await LoadAsync(context, cancellationToken).ConfigureAwait(false)).Days;
    }

    private static Task<AnimalNutritionDto> LoadAsync(ReportContext context, CancellationToken cancellationToken)
    {
        var range = context.Range("period");
        return context.Send(new GetAnimalNutritionQuery(context.Id("animalId"), range.From, range.To), cancellationToken);
    }
}
