using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Analytics.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Health;

/// <summary>
/// One line of the mortality &amp; morbidity analysis. Each section fills the columns that mean
/// something for it and leaves the rest blank: a cause of death has deaths, a diagnosis has cases.
/// </summary>
public sealed record MortalityMorbidityReportRow(
    string Section,
    string Item,
    int? Deaths,
    decimal? AverageHead,
    decimal? MortalityRate,
    int? Incidents,
    int? AnimalsAffected,
    int? Treatments,
    decimal? TreatmentCostBdt);

/// <summary>The mortality rule, public so the denominator is tested rather than described.</summary>
public static class MortalityMath
{
    /// <summary>
    /// Deaths ÷ the average number of animals on the farm over the same days (head-days ÷ days, see
    /// <see cref="HerdMetrics.AverageHeadCount"/>) — not the closing head count, which the deaths
    /// themselves have already reduced, and not the opening one, which ignores every arrival.
    /// Returned as a ratio (0.025 = 2.5 %). Null when no animal was on the farm.
    /// </summary>
    public static decimal? Rate(int deaths, decimal averageHead) =>
        averageHead <= 0m ? null : decimal.Round(deaths / averageHead, 6);

    /// <summary>
    /// A death for mortality purposes: status Dead with a death date in the window, and not a
    /// slaughter (a slaughter is a disposal, booked at cost like a sale).
    /// </summary>
    public static bool IsMortality(Animal animal, MortalityRecord? record, DateOnly from, DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return animal.Status == AnimalStatus.Dead
               && animal.DeathDate is { } died && died >= from && died <= to
               && animal.DisposalReason != DisposalReason.Slaughter
               && record?.CauseOfDeath != CauseOfDeath.Slaughter;
    }

    /// <summary>How long an animal was on the farm (exit exclusive), the input to the head-day count.</summary>
    public static HerdMetrics.Residency Residency(Animal animal)
    {
        ArgumentNullException.ThrowIfNull(animal);
        return new HerdMetrics.Residency(
            animal.AcquisitionDate,
            HerdMetrics.ExitDate(animal.Status, animal.SaleDate, animal.DeathDate, animal.ModifiedAtUtc));
    }
}

/// <summary>
/// Report H5 — mortality &amp; morbidity: deaths in the period by cause, month, batch and shed with
/// the mortality rate against the average on-farm head count, then disease incidents and treatments
/// by diagnosis with the treatment cost.
/// </summary>
/// <remarks>
/// <para>
/// Each section is a complete breakdown of the same deaths (or cases), so every section sub-totals
/// to the farm figure and a grand total across sections would count each death several times —
/// there is none. Rates are never added; each row's rate uses its own denominator.
/// </para>
/// <para>
/// Batch is the animal's batch as recorded now (batch membership has no history). Shed is
/// reconstructed from the dated shed placements, so an animal moved before it died is counted in the
/// shed it died in, and each shed's head count uses the days animals actually spent there.
/// </para>
/// </remarks>
public sealed class MortalityMorbidityReportDefinition : ReportDefinition<MortalityMorbidityReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IMortalityRecordRepository _mortality;
    private readonly IDiseaseIncidentRepository _incidents;
    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IAnimalBatchRepository _batches;
    private readonly IShedRepository _sheds;

    public MortalityMorbidityReportDefinition(
        IAnimalRepository animals,
        IMortalityRecordRepository mortality,
        IDiseaseIncidentRepository incidents,
        IMedicalTreatmentRepository treatments,
        IAnimalBatchRepository batches,
        IShedRepository sheds)
    {
        _animals = animals;
        _mortality = mortality;
        _incidents = incidents;
        _treatments = treatments;
        _batches = batches;
        _sheds = sheds;
    }

    public override string Key => "health.mortality-morbidity";

    public override LocalizedText Title => new("Deaths & Illness", "মৃত্যু ও অসুস্থতা");

    public override LocalizedText Description => new(
        "How many animals died or fell ill, and why? Deaths by cause, month, batch and shed with the death rate, then illness cases and treatments by disease with their cost.",
        "কতগুলো পশু মারা গেছে বা অসুস্থ হয়েছে, এবং কেন? কারণ, মাস, ব্যাচ ও শেড অনুযায়ী মৃত্যু ও মৃত্যুহার, তারপর রোগ অনুযায়ী অসুস্থতা, চিকিৎসা ও খরচ।");

    public override ReportCategory Category => ReportCategory.Health;

    public override PageSetup Page => PageSetup.A4Landscape;

    // Every section re-counts the same deaths; a grand total would multiply them.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "fiscal-year-to-date"),
    ];

    public override IReadOnlyList<ReportColumn<MortalityMorbidityReportRow>> Columns =>
    [
        ReportColumn<MortalityMorbidityReportRow>.Text("item", new LocalizedText("Cause / month / batch / shed / disease", "কারণ / মাস / ব্যাচ / শেড / রোগ"), r => r.Item, width: 3f),
        ReportColumn<MortalityMorbidityReportRow>.WholeNumber("deaths", new LocalizedText("Deaths (head)", "মৃত্যু (টি)"), r => r.Deaths, widthMm: 18f, aggregate: ReportAggregate.Sum),
        ReportColumn<MortalityMorbidityReportRow>.Number("avgHead", new LocalizedText("Average animals on farm (head)", "খামারে গড় পশু (টি)"), r => r.AverageHead, 1, 26f),
        ReportColumn<MortalityMorbidityReportRow>.Percent("rate", new LocalizedText("Death rate (%)", "মৃত্যুহার (%)"), r => r.MortalityRate, 2, 20f),
        ReportColumn<MortalityMorbidityReportRow>.WholeNumber("incidents", new LocalizedText("Illness cases", "অসুস্থতার ঘটনা"), r => r.Incidents, widthMm: 20f, aggregate: ReportAggregate.Sum),
        ReportColumn<MortalityMorbidityReportRow>.WholeNumber("affected", new LocalizedText("Animals affected (head)", "আক্রান্ত পশু (টি)"), r => r.AnimalsAffected, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<MortalityMorbidityReportRow>.WholeNumber("treatments", new LocalizedText("Treatments given", "চিকিৎসা দেওয়া হয়েছে"), r => r.Treatments, widthMm: 22f, aggregate: ReportAggregate.Sum),
        ReportColumn<MortalityMorbidityReportRow>.Money("cost", new LocalizedText("Treatment cost (BDT)", "চিকিৎসা খরচ (টাকা)"), r => r.TreatmentCostBdt, widthMm: 28f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<MortalityMorbidityReportRow>? Group =>
        ReportGroup<MortalityMorbidityReportRow>.By(r => r.Section, r => r.Section);

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each section breaks down the same deaths (or the same illness cases) a different way, so each section adds up to the whole-farm line; sections are not added together.",
            "প্রতিটি অংশ একই মৃত্যু (বা একই অসুস্থতা) ভিন্নভাবে ভাগ করে দেখায়, তাই প্রতিটি অংশের যোগফল সমগ্র খামারের লাইনের সমান; অংশগুলো একসাথে যোগ করা হয় না।"),
        new("Death rate = deaths ÷ average number of animals on the farm over the same days; each row uses its own average (its month, batch or shed).",
            "মৃত্যুহার = মৃত্যু ÷ একই সময়ে খামারে গড় পশুর সংখ্যা; প্রতিটি সারি নিজের গড় ব্যবহার করে (তার মাস, ব্যাচ বা শেড)।"),
        new("Average animals counts sold or dead animals only for the days they were on the farm. Slaughter is a sale, not a death.",
            "গড় পশুর হিসাবে বিক্রি হওয়া বা মৃত পশু শুধু খামারে থাকার দিনগুলোর জন্য ধরা হয়েছে। জবাই বিক্রি হিসেবে ধরা হয়, মৃত্যু নয়।"),
        new("Batch is the animal's batch today; shed is the shed it was in when it died.",
            "ব্যাচ হলো পশুটির আজকের ব্যাচ; শেড হলো মৃত্যুর সময় যে শেডে ছিল।"),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rows = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var farm = rows.Count > 0 ? rows[0] : null;
        if (farm is null)
        {
            return null;
        }

        string N(decimal v, int d) => ReportValueFormatter.Format(v, d == 0 ? ReportColumnType.WholeNumber : ReportColumnType.Number, d, context.BengaliNumerals);
        var rate = farm.MortalityRate is { } r ? N(r * 100m, 2) + "%" : "—";

        var period = context.Range("period");
        var range = $"{period.From.ToString("d MMM yyyy", CultureInfo.InvariantCulture)} – {period.To.ToString("d MMM yyyy", CultureInfo.InvariantCulture)}";

        return context.Language == ReportLanguage.Bn
            ? $"{range} · মৃত্যু {N(farm.Deaths ?? 0, 0)} টি · খামারে গড় {N(farm.AverageHead ?? 0m, 1)} টি পশু · মৃত্যুহার {rate} · চিকিৎসা {N(farm.Treatments ?? 0, 0)} টি"
            : $"{range} · {N(farm.Deaths ?? 0, 0)} deaths · average {N(farm.AverageHead ?? 0m, 1)} animals on farm · death rate {rate} · {N(farm.Treatments ?? 0, 0)} treatments";
    }

    protected override Task<IReadOnlyList<MortalityMorbidityReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LoadAsync(context, cancellationToken);
    }

    private Task<IReadOnlyList<MortalityMorbidityReportRow>> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync<IReadOnlyList<MortalityMorbidityReportRow>>("mortality-morbidity", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var (from, to) = (period.From, period.To);
            var bn = context.Language == ReportLanguage.Bn;
            string T(string en, string bnText) => bn ? bnText : en;

            var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false);
            var records = (await _mortality.GetByFarmBetweenAsync(farmId, from, to, cancellationToken).ConfigureAwait(false))
                .GroupBy(m => m.AnimalId)
                .ToDictionary(g => g.Key, g => g.First());
            var incidents = await _incidents.GetByFarmBetweenAsync(farmId, from, to, cancellationToken).ConfigureAwait(false);
            var treatments = await _treatments.GetByFarmStartedBetweenAsync(farmId, from, to, cancellationToken).ConfigureAwait(false);

            var residency = animals.ToDictionary(a => a.Id, MortalityMath.Residency);
            var deaths = animals
                .Where(a => MortalityMath.IsMortality(a, records.GetValueOrDefault(a.Id), from, to))
                .ToList();
            var dead = deaths.Select(a => a.Id).ToHashSet();

            var rows = new List<MortalityMorbidityReportRow>();
            MortalityMorbidityReportRow DeathRow(string section, string item, int count, decimal avgHead) =>
                new(section, item, count, decimal.Round(avgHead, 2), MortalityMath.Rate(count, avgHead), null, null, null, null);

            // 1. Whole farm.
            var farmAvg = HerdMetrics.AverageHeadCount(residency.Values, from, to);
            rows.Add(new MortalityMorbidityReportRow(
                T("1. Whole farm", "১. সমগ্র খামার"), T("All animals", "সকল পশু"),
                deaths.Count, decimal.Round(farmAvg, 2), MortalityMath.Rate(deaths.Count, farmAvg),
                incidents.Count, incidents.Sum(i => i.AffectedAnimalCount),
                treatments.Count, treatments.Sum(t => t.CostBdt)));

            // 2. By cause — the rate of each cause is against the whole farm's head count.
            var byCause = T("2. Deaths by cause", "২. কারণ অনুযায়ী মৃত্যু");
            foreach (var g in deaths
                         .GroupBy(a => Cause(a, records.GetValueOrDefault(a.Id), bn), StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(DeathRow(byCause, g.Key, g.Count(), farmAvg));
            }

            if (deaths.Count == 0)
            {
                rows.Add(DeathRow(byCause, T("No deaths recorded", "কোনো মৃত্যু নেই"), 0, farmAvg));
            }

            // 3. By month — each month against its own average head count.
            var byMonth = T("3. Deaths by month", "৩. মাস অনুযায়ী মৃত্যু");
            for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            {
                var start = month < from ? from : month;
                var monthEnd = month.AddMonths(1).AddDays(-1);
                var end = monthEnd > to ? to : monthEnd;
                var count = deaths.Count(a => a.DeathDate >= start && a.DeathDate <= end);
                var monthLabel = bn
                    ? $"{BanglaMonths[month.Month - 1]} {month.Year.ToString(CultureInfo.InvariantCulture)}"
                    : month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
                rows.Add(DeathRow(byMonth, monthLabel, count,
                    HerdMetrics.AverageHeadCount(residency.Values, start, end)));
            }

            // 4. By batch (current batch membership).
            var batches = (await _batches.GetPagedAsync(1, 10_000, farmId, null, cancellationToken).ConfigureAwait(false)).Items
                .ToDictionary(b => b.Id, b => b.Name);
            var noBatch = T("Not in a batch", "ব্যাচবিহীন");
            var byBatch = T("4. Deaths by batch", "৪. ব্যাচ অনুযায়ী মৃত্যু");
            foreach (var g in animals
                         .Where(a => residency[a.Id] is var r && r.Entry <= to && (r.Exit is null || r.Exit > from))
                         .GroupBy(a => a.BatchId is { } id && batches.TryGetValue(id, out var name) ? name : noBatch, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var avg = HerdMetrics.AverageHeadCount(g.Select(a => residency[a.Id]), from, to);
                rows.Add(DeathRow(byBatch, g.Key, g.Count(a => dead.Contains(a.Id)), avg));
            }

            // 5. By shed, from the dated shed placements.
            var sheds = (await _sheds.GetAllByFarmAsync(context.TenantId, farmId, cancellationToken).ConfigureAwait(false))
                .ToDictionary(s => s.Id, s => string.IsNullOrWhiteSpace(s.ShedName) ? s.ShedNumber : $"{s.ShedNumber} · {s.ShedName}");
            var movements = await _animals.GetMovementsByFarmAsync(farmId, cancellationToken).ConfigureAwait(false);
            rows.AddRange(ShedRows(T("5. Deaths by shed", "৫. শেড অনুযায়ী মৃত্যু"), T("No shed", "শেডবিহীন"),
                animals, residency, deaths, movements, sheds, context.TimeZone, from, to));

            // 6. Disease incidents by diagnosis.
            var byDisease = T("6. Illness cases by disease", "৬. রোগ অনুযায়ী অসুস্থতার ঘটনা");
            foreach (var g in incidents
                         .GroupBy(i => Clean(i.DiseaseName), StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new MortalityMorbidityReportRow(byDisease, g.Key, null, null, null, g.Count(), g.Sum(i => i.AffectedAnimalCount), null, null));
            }

            if (incidents.Count == 0)
            {
                rows.Add(new MortalityMorbidityReportRow(byDisease, T("No illness recorded", "কোনো অসুস্থতা নেই"), null, null, null, 0, 0, null, null));
            }

            // 7. Treatments by diagnosis.
            var byDiagnosis = T("7. Treatments by disease", "৭. রোগ অনুযায়ী চিকিৎসা");
            foreach (var g in treatments
                         .GroupBy(t => Clean(t.Diagnosis), StringComparer.OrdinalIgnoreCase)
                         .OrderByDescending(g => g.Sum(t => t.CostBdt)).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new MortalityMorbidityReportRow(byDiagnosis, g.Key, null, null, null, null, g.Select(t => t.AnimalId).Distinct().Count(), g.Count(), g.Sum(t => t.CostBdt)));
            }

            if (treatments.Count == 0)
            {
                rows.Add(new MortalityMorbidityReportRow(byDiagnosis, T("No treatments recorded", "কোনো চিকিৎসা নেই"), null, null, null, null, 0, 0, 0m));
            }

            return rows;
        });

    private static List<MortalityMorbidityReportRow> ShedRows(
        string section,
        string noShed,
        IReadOnlyList<Animal> animals,
        Dictionary<Guid, HerdMetrics.Residency> residency,
        List<Animal> deaths,
        IReadOnlyList<(Guid AnimalId, Guid? ShedId, DateTime PlacedAtUtc, DateTime? RemovedAtUtc)> movements,
        Dictionary<Guid, string> sheds,
        TimeZoneInfo zone,
        DateOnly from,
        DateOnly to)
    {
        DateOnly Local(DateTime utc) => ReportTimeZone.Today(utc, zone);
        var byAnimal = movements.ToLookup(m => m.AnimalId);

        // Each placement is a stay in one shed, clipped to the animal's own time on the farm.
        var stays = new List<(Guid? ShedId, HerdMetrics.Residency Stay)>();
        foreach (var animal in animals)
        {
            var life = residency[animal.Id];
            var placements = byAnimal[animal.Id].ToList();
            if (placements.Count == 0)
            {
                stays.Add((null, life));
                continue;
            }

            foreach (var m in placements)
            {
                var entry = Local(m.PlacedAtUtc) < life.Entry ? life.Entry : Local(m.PlacedAtUtc);
                DateOnly? exit = m.RemovedAtUtc is { } removed ? Local(removed) : life.Exit;
                if (life.Exit is { } left && (exit is null || exit > left))
                {
                    exit = left;
                }

                stays.Add((m.ShedId, new HerdMetrics.Residency(entry, exit)));
            }
        }

        // Where each dead animal was: its latest placement made on or before the death date.
        var deathShed = deaths.ToDictionary(
            a => a.Id,
            a => byAnimal[a.Id]
                .Where(m => Local(m.PlacedAtUtc) <= a.DeathDate)
                .OrderByDescending(m => m.PlacedAtUtc)
                .Select(m => m.ShedId)
                .FirstOrDefault());

        string Name(Guid? id) => id is { } s && sheds.TryGetValue(s, out var name) ? name : noShed;

        return stays
            .GroupBy(s => Name(s.ShedId), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var avg = HerdMetrics.AverageHeadCount(g.Select(s => s.Stay), from, to);
                var count = deathShed.Values.Count(id => string.Equals(Name(id), g.Key, StringComparison.OrdinalIgnoreCase));
                return (Name: g.Key, Count: count, Avg: avg);
            })
            .Where(x => x.Avg > 0m || x.Count > 0)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new MortalityMorbidityReportRow(section, x.Name, x.Count, decimal.Round(x.Avg, 2), MortalityMath.Rate(x.Count, x.Avg), null, null, null, null))
            .ToList();
    }

    private static string Cause(Animal animal, MortalityRecord? record, bool bn)
    {
        if (record is not null)
        {
            var cause = record.CauseOfDeath switch
            {
                CauseOfDeath.Disease => bn ? "রোগ" : "Disease",
                CauseOfDeath.Accident => bn ? "দুর্ঘটনা" : "Accident",
                CauseOfDeath.NaturalCauses => bn ? "স্বাভাবিক মৃত্যু" : "Natural causes",
                _ => bn ? "অজানা" : "Unknown",
            };
            return string.IsNullOrWhiteSpace(record.DiseaseName) ? cause : $"{cause} — {record.DiseaseName.Trim()}";
        }

        return animal.DisposalReason switch
        {
            DisposalReason.Disease => bn ? "রোগ" : "Disease",
            DisposalReason.Accident => bn ? "দুর্ঘটনা" : "Accident",
            DisposalReason.NaturalDeath => bn ? "স্বাভাবিক মৃত্যু" : "Natural causes",
            _ => bn ? "অজানা" : "Unknown",
        };
    }

    private static readonly string[] BanglaMonths =
    [
        "জানুয়ারি", "ফেব্রুয়ারি", "মার্চ", "এপ্রিল", "মে", "জুন",
        "জুলাই", "আগস্ট", "সেপ্টেম্বর", "অক্টোবর", "নভেম্বর", "ডিসেম্বর",
    ];

    private static string Clean(string? text) => string.IsNullOrWhiteSpace(text) ? "—" : text.Trim();
}
