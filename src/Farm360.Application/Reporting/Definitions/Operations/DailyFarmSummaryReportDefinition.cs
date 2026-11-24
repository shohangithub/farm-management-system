using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Operations;

/// <summary>
/// One line of the day's summary. A count or quantity goes in <paramref name="Quantity"/> with its
/// unit, money in <paramref name="AmountBdt"/> — never both in one column.
/// </summary>
public sealed record DailyFarmSummaryReportRow(
    string Section,
    string Item,
    decimal? Quantity,
    string Unit,
    decimal? AmountBdt,
    string Notes);

/// <summary>
/// Report OP1 — the one-page daily farm summary: herd movements (opening head, arrivals, sales,
/// deaths, closing head), feedings done against planned with feed issued, treatments and
/// vaccinations given, stock alerts and the day's cash in and out.
/// </summary>
/// <remarks>
/// <para>
/// Head counts follow the farm rule (<see cref="Domain.Livestock.Animal.IsOnFarmOn"/>): opening is
/// the herd on the previous day, closing the herd on this day, so opening + arrivals − sales −
/// deaths = closing. Feed issued is the confirmed or adjusted feeding entries of the day.
/// </para>
/// <para>
/// The report needs the livestock permission; the cash lines additionally need the finance
/// permission and are replaced by a note for anyone without it. Cash comes from the ledger's
/// cash-affecting postings of the day, and the closing cash balance is the balance sheet's.
/// </para>
/// </remarks>
public sealed class DailyFarmSummaryReportDefinition : ReportDefinition<DailyFarmSummaryReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IDailyFeedingEntryRepository _feedingEntries;
    private readonly IMedicalTreatmentRepository _treatments;
    private readonly IVaccinationRepository _vaccinations;
    private readonly IInventoryItemRepository _items;
    private readonly IFinancialStatementEngine _engine;
    private readonly IPermissionService _permissions;

    public DailyFarmSummaryReportDefinition(
        IAnimalRepository animals,
        IDailyFeedingEntryRepository feedingEntries,
        IMedicalTreatmentRepository treatments,
        IVaccinationRepository vaccinations,
        IInventoryItemRepository items,
        IFinancialStatementEngine engine,
        IPermissionService permissions)
    {
        _animals = animals;
        _feedingEntries = feedingEntries;
        _treatments = treatments;
        _vaccinations = vaccinations;
        _items = items;
        _engine = engine;
        _permissions = permissions;
    }

    public override string Key => "operations.daily-farm-summary";

    public override LocalizedText Title => new("Daily Farm Summary", "দৈনিক খামার সারসংক্ষেপ");

    public override LocalizedText Description => new(
        "What happened on the farm today? Animals in and out, feedings done against plan, feed used, treatments and vaccinations, items running low and cash in and out — on one page.",
        "আজ খামারে কী হয়েছে? পশু আসা-যাওয়া, পরিকল্পনার তুলনায় খাওয়ানো, ব্যবহৃত খাদ্য, চিকিৎসা ও টিকা, ফুরিয়ে আসা মালামাল এবং নগদ আয়-ব্যয় — এক পাতায়।");

    public override ReportCategory Category => ReportCategory.Operations;

    // Operations has no module permission of its own; the summary is a herd document.
    public override string? Permission => ReportPermissions.LivestockView;

    public override PageSetup Page => PageSetup.A4Portrait;

    // Heads, kilograms and taka on one page: no column of this statement adds up to anything.
    public override bool ShowGrandTotals => false;

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.Date("date", new LocalizedText("Date", "তারিখ"), required: true, @default: "today"),
    ];

    public override IReadOnlyList<ReportColumn<DailyFarmSummaryReportRow>> Columns =>
    [
        ReportColumn<DailyFarmSummaryReportRow>.Text("item", new LocalizedText("Item", "বিষয়"), r => r.Item, width: 3f),
        ReportColumn<DailyFarmSummaryReportRow>.Number("qty", new LocalizedText("Count / quantity", "সংখ্যা / পরিমাণ"), r => r.Quantity, 2, 26f),
        ReportColumn<DailyFarmSummaryReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<DailyFarmSummaryReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "টাকার পরিমাণ (টাকা)"), r => r.AmountBdt, widthMm: 28f),
        ReportColumn<DailyFarmSummaryReportRow>.Text("notes", new LocalizedText("Details", "বিস্তারিত"), r => r.Notes, width: 2.4f),
    ];

    public override ReportGroup<DailyFarmSummaryReportRow>? Group =>
        ReportGroup<DailyFarmSummaryReportRow>.By(r => r.Section, r => r.Section);

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Animals: start of day + arrivals − sold − died = end of day; any difference is shown as exits without a recorded date.",
            "পশু: দিনের শুরু + আগমন − বিক্রি − মৃত্যু = দিনের শেষ; কোনো পার্থক্য থাকলে তা তারিখবিহীন প্রস্থান হিসেবে দেখানো হয়।"),
        new("Feed used counts only feedings marked done (confirmed or adjusted), at the feed's cost on the day it was used.",
            "ব্যবহৃত খাদ্যে শুধু সম্পন্ন (নিশ্চিত বা সমন্বিত) খাওয়ানো ধরা হয়েছে, ব্যবহারের দিনের খাদ্য দরে।"),
        new("Items running low are as of now, not as of the report date.",
            "ফুরিয়ে আসা মালামালের তালিকা এখনকার অবস্থা, প্রতিবেদনের তারিখের নয়।"),
        new("Cash lines appear only for users allowed to see finance; the end-of-day balance matches the balance sheet.",
            "নগদের লাইন শুধু আর্থিক তথ্য দেখার অনুমতি থাকলে দেখা যায়; দিনের শেষের স্থিতি উদ্বৃত্তপত্রের সাথে মেলে।"),
    ];

    public override Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var day = context.Date("date");
        var date = ReportValueFormatter.Format(day, ReportColumnType.Date, 0, context.BengaliNumerals, context.Language);
        return Task.FromResult<string?>(context.Language == ReportLanguage.Bn
            ? $"{date} তারিখের সারসংক্ষেপ"
            : $"Summary for {day.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture)}, {date}");
    }

    protected override async Task<IReadOnlyList<DailyFarmSummaryReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var farmId = context.FarmId();
        var day = context.Date("date");
        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;
        var head = T("head", "টি");
        var count = T("count", "টি");
        var kg = T("kg", "কেজি");

        var rows = new List<DailyFarmSummaryReportRow>();
        void Add(string section, string item, decimal? qty, string unit, decimal? amount, string notes = "") =>
            rows.Add(new DailyFarmSummaryReportRow(section, item, qty, unit, amount, notes));

        // ── 1. Herd ─────────────────────────────────────────────────────────
        var herd = T("1. Animals", "১. পশু");
        var animals = await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false);
        var opening = animals.Count(a => a.IsOnFarmOn(day.AddDays(-1)));
        var arrivals = animals.Where(a => a.AcquisitionDate == day).ToList();
        var sales = animals.Where(a => a.Status == AnimalStatus.Sold && a.SaleDate == day).ToList();
        var deaths = animals.Where(a => a.Status == AnimalStatus.Dead && a.DeathDate == day).ToList();
        var closing = animals.Count(a => a.IsOnFarmOn(day));

        Add(herd, T("On farm at start of day", "দিনের শুরুতে খামারে"), opening, head, null);
        Add(herd, T("(+) Arrivals (bought / born)", "(+) আগমন (ক্রয় / জন্ম)"), arrivals.Count, head, arrivals.Sum(a => a.AcquisitionPriceBdt ?? 0m),
            Tags(arrivals.Select(a => a.Tag.TagId)));
        Add(herd, T("(−) Sold", "(−) বিক্রি"), sales.Count, head, sales.Sum(a => a.SalePriceBdt ?? 0m),
            Tags(sales.Select(a => a.Tag.TagId)));
        Add(herd, T("(−) Died", "(−) মৃত"), deaths.Count, head, null, Tags(deaths.Select(a => a.Tag.TagId)));
        var unexplained = opening + arrivals.Count - sales.Count - deaths.Count - closing;
        Add(herd, T("On farm at end of day", "দিনের শেষে খামারে"), closing, head, null,
            unexplained == 0 ? string.Empty : T($"{unexplained} other exit(s) without a recorded date", $"তারিখবিহীন অন্যান্য প্রস্থান {unexplained} টি"));

        // ── 2. Feeding ──────────────────────────────────────────────────────
        var feeding = T("2. Feeding", "২. খাওয়ানো");
        var entries = await _feedingEntries.GetEntriesByDateAsync(context.TenantId, farmId, day, cancellationToken).ConfigureAwait(false);
        var done = entries.Where(e => e.Status is DailyFeedingEntryStatus.Confirmed or DailyFeedingEntryStatus.Adjusted).ToList();
        var skipped = entries.Count(e => e.Status == DailyFeedingEntryStatus.Skipped);
        var open = entries.Count(e => e.Status is DailyFeedingEntryStatus.Pending or DailyFeedingEntryStatus.Exception);

        Add(feeding, T("Feedings planned", "পরিকল্পিত খাদ্য প্রদান"), entries.Count, count, null);
        Add(feeding, T("Feedings done (confirmed / adjusted)", "সম্পন্ন (নিশ্চিত / সমন্বিত)"), done.Count, count, null,
            entries.Count == 0 ? string.Empty : T($"{Percent(done.Count, entries.Count)} of planned", $"পরিকল্পনার {Percent(done.Count, entries.Count)}"));
        Add(feeding, T("Feedings skipped", "বাদ দেওয়া"), skipped, count, null);
        Add(feeding, T("Feedings still pending / exceptions", "এখনো বাকি / ব্যতিক্রম"), open, count, null,
            open > 0 ? T("Confirm or skip on the feeding board", "খাদ্য বোর্ডে নিশ্চিত বা বাদ দিন") : string.Empty);
        Add(feeding, T("Feed planned", "পরিকল্পিত খাদ্য"), entries.Sum(e => e.ExpectedKg), kg, null);
        Add(feeding, T("Feed used (done feedings)", "ব্যবহৃত খাদ্য (সম্পন্ন)"), done.Sum(e => e.ActualKg ?? e.ExpectedKg), kg,
            done.Sum(e => e.TotalCostBdt ?? 0m), T("Cost at the day's feed price", "ব্যবহারের দিনের দরে খরচ"));

        // ── 3. Health ───────────────────────────────────────────────────────
        var health = T("3. Health", "৩. স্বাস্থ্য");
        var treatments = await _treatments.GetByFarmStartedBetweenAsync(farmId, day, day, cancellationToken).ConfigureAwait(false);
        var vaccinations = await _vaccinations.GetAdministeredByFarmAsync(farmId, day, day, cancellationToken).ConfigureAwait(false);
        Add(health, T("Treatments started", "শুরু হওয়া চিকিৎসা"), treatments.Count, count, treatments.Sum(t => t.CostBdt),
            string.Join(", ", treatments.Select(t => t.MedicationName).Distinct(StringComparer.OrdinalIgnoreCase).Take(5)));
        Add(health, T("Vaccinations given", "প্রদত্ত টিকা"), vaccinations.Count, count, null,
            string.Join(", ", vaccinations.Select(v => v.Event.VaccineName).Distinct(StringComparer.OrdinalIgnoreCase).Take(5)));

        // ── 4. Stock alerts ─────────────────────────────────────────────────
        var stock = T("4. Items running low (now)", "৪. ফুরিয়ে আসা মালামাল (এখন)");
        var low = await _items.GetLowStockItemsAsync(farmId, cancellationToken).ConfigureAwait(false);
        if (low.Count == 0)
        {
            Add(stock, T("No item at or below its reorder level", "পুনঃক্রয় সীমার নিচে কোনো পণ্য নেই"), null, string.Empty, null);
        }

        foreach (var item in low.OrderBy(i => i.CurrentStock <= 0m ? 0 : 1).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            Add(stock, item.Name, item.CurrentStock, item.UnitOfMeasure, null,
                item.CurrentStock <= 0m
                    ? T("Out of stock", "মজুদ শেষ")
                    : T($"Reorder when at or below {item.ReorderThreshold:0.##} {item.UnitOfMeasure}", $"{item.ReorderThreshold:0.##} {item.UnitOfMeasure} বা কম হলে আবার কিনুন"));
        }

        // ── 5. Cash ─────────────────────────────────────────────────────────
        var cash = T("5. Cash & bank", "৫. নগদ ও ব্যাংক");
        if (await CanSeeFinanceAsync(context, cancellationToken).ConfigureAwait(false))
        {
            var ledger = await _engine.LoadAsync(farmId, day, cancellationToken).ConfigureAwait(false);
            var postings = ledger.PostingsBetween(day, day).Where(t => t.AffectsCash).ToList();
            var cashIn = AccountChart.CashIn(postings);
            var cashOut = AccountChart.CashOut(postings);
            Add(cash, T("Cash received", "নগদ প্রাপ্তি"), postings.Count(p => AccountChart.CashEffect(p) > 0), count, cashIn);
            Add(cash, T("Cash paid", "নগদ প্রদান"), postings.Count(p => AccountChart.CashEffect(p) < 0), count, cashOut);
            Add(cash, T("Net cash flow", "নিট নগদ প্রবাহ"), null, string.Empty, cashIn - cashOut);
            Add(cash, T("Cash & bank balance at end of day", "দিনের শেষে নগদ ও ব্যাংক স্থিতি"), null, string.Empty,
                ledger.BalancesAt(day).GetValueOrDefault(AccountChart.CashAccount), T("As on the balance sheet", "উদ্বৃত্তপত্র অনুযায়ী"));
        }
        else
        {
            Add(cash, T("Hidden — requires the finance permission", "লুকানো — আর্থিক অনুমতি প্রয়োজন"), null, string.Empty, null);
        }

        return rows;
    }

    private async Task<bool> CanSeeFinanceAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.UserId is { } userId
        && await _permissions.HasPermissionAsync(userId, context.TenantId, ReportPermissions.FinanceView, cancellationToken).ConfigureAwait(false);

    private static string Percent(int part, int whole) =>
        whole == 0 ? "—" : (part * 100m / whole).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";

    private static string Tags(IEnumerable<string> tags)
    {
        var list = tags.ToList();
        return list.Count switch
        {
            0 => string.Empty,
            <= 6 => string.Join(", ", list),
            _ => string.Join(", ", list.Take(6)) + $" +{list.Count - 6}",
        };
    }
}
