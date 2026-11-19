using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
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

/// <summary>
/// One sale. Animal sales carry weight, cost to date and profit; other sales (milk, by-products,
/// other income) carry none of those — they are left blank, never shown as zero.
/// </summary>
public sealed record SalesRegisterReportRow(
    string Section,
    DateOnly Date,
    string Reference,
    string Item,
    string Buyer,
    decimal? SaleWeightKg,
    decimal PriceBdt,
    decimal? PricePerKgBdt,
    decimal? CostToDateBdt,
    decimal? ProfitBdt,
    string Terms,
    decimal OutstandingBdt);

/// <summary>
/// Report FN12 — sales register: every animal sold in the period with buyer, sale weight, price,
/// price per kg, the animal's full cost to date and the profit on it, cash or credit and what the
/// buyer still owes; then the farm's other sales (milk, by-products, other income) from the ledger.
/// </summary>
/// <remarks>
/// <para>
/// Cost to date is <see cref="IAnimalValuationService.CostToDate"/> — acquisition, feed, health,
/// labour and overhead from the animal's cost ledger — the same figure the herd, batch P&amp;L and
/// valuation reports print, so profit here is price − that cost and nothing else.
/// </para>
/// <para>
/// Other sales come from the financial statement engine's postings for the period, signed by their
/// profit effect, so the total of every sale equals the revenue lines of the income statement.
/// Credit and amount outstanding come from the receivable bill raised for the sale (today's balance).
/// </para>
/// </remarks>
public sealed class SalesRegisterReportDefinition : ReportDefinition<SalesRegisterReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalValuationService _valuation;
    private readonly ITradeBillRepository _bills;
    private readonly IFinancialStatementEngine _engine;

    public SalesRegisterReportDefinition(
        IAnimalRepository animals,
        IBreedRepository breeds,
        IAnimalFeedAllocationRepository feed,
        IAnimalCostLedgerRepository ledgers,
        IAnimalValuationService valuation,
        ITradeBillRepository bills,
        IFinancialStatementEngine engine)
    {
        _animals = animals;
        _breeds = breeds;
        _feed = feed;
        _ledgers = ledgers;
        _valuation = valuation;
        _bills = bills;
        _engine = engine;
    }

    public override string Key => "finance.sales-register";

    public override LocalizedText Title => new("Sales Register", "বিক্রয় রেজিস্টার");

    public override LocalizedText Description => new(
        "Every animal sold in the period with buyer, sale weight, price, price/kg, cost to date and profit, cash or credit and amount outstanding; plus milk, by-product and other sales.",
        "নির্বাচিত সময়ে বিক্রিত প্রতিটি পশু — ক্রেতা, বিক্রয় ওজন, মূল্য, প্রতি কেজি দর, মোট খরচ ও লাভ, নগদ/বাকি ও বকেয়া; সাথে দুধ, উপজাত ও অন্যান্য বিক্রয়।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total sales", "মোট বিক্রয়");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-month"),
        ReportParameter.Bool("includeOtherSales", new LocalizedText("Include milk, by-product and other sales", "দুধ, উপজাত ও অন্যান্য বিক্রয় অন্তর্ভুক্ত করুন"), @default: true),
    ];

    public override IReadOnlyList<ReportColumn<SalesRegisterReportRow>> Columns =>
    [
        ReportColumn<SalesRegisterReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<SalesRegisterReportRow>.Text("ref", new LocalizedText("Tag / Voucher", "ট্যাগ / ভাউচার"), r => r.Reference, width: 1.3f),
        ReportColumn<SalesRegisterReportRow>.Text("item", new LocalizedText("Breed / Item", "জাত / পণ্য"), r => r.Item, width: 1.5f),
        ReportColumn<SalesRegisterReportRow>.Text("buyer", new LocalizedText("Buyer", "ক্রেতা"), r => r.Buyer, width: 1.6f),
        ReportColumn<SalesRegisterReportRow>.Number("weight", new LocalizedText("Sale weight (kg)", "বিক্রয় ওজন (কেজি)"), r => r.SaleWeightKg, 1, 20f, ReportAggregate.Sum),
        ReportColumn<SalesRegisterReportRow>.Money("price", new LocalizedText("Price (BDT)", "মূল্য (টাকা)"), r => r.PriceBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<SalesRegisterReportRow>.Money("pricePerKg", new LocalizedText("Price / kg", "প্রতি কেজি দর"), r => r.PricePerKgBdt, widthMm: 20f),
        ReportColumn<SalesRegisterReportRow>.Money("cost", new LocalizedText("Cost to date", "মোট খরচ"), r => r.CostToDateBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<SalesRegisterReportRow>.Money("profit", new LocalizedText("Profit / (loss)", "লাভ / (ক্ষতি)"), r => r.ProfitBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<SalesRegisterReportRow>.Text("terms", new LocalizedText("Cash / Credit", "নগদ / বাকি"), r => r.Terms, width: 16f, relative: false),
        ReportColumn<SalesRegisterReportRow>.Money("outstanding", new LocalizedText("Outstanding (today)", "বকেয়া (আজ)"), r => r.OutstandingBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<SalesRegisterReportRow>? Group =>
        ReportGroup<SalesRegisterReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rows = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        var animalRows = rows.Where(r => r.CostToDateBdt is not null).ToList();
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals);
        string N(int v) => ReportValueFormatter.Format(v, ReportColumnType.WholeNumber, 0, context.BengaliNumerals);

        var revenue = animalRows.Sum(r => r.PriceBdt);
        var profit = animalRows.Sum(r => r.ProfitBdt ?? 0m);
        var outstanding = rows.Sum(r => r.OutstandingBdt);

        return context.Language == ReportLanguage.Bn
            ? $"{N(animalRows.Count)} টি পশু {M(revenue)} টাকায় বিক্রি · লাভ {M(profit)} · মোট বকেয়া {M(outstanding)}"
            : $"{N(animalRows.Count)} animals sold for {M(revenue)} BDT · profit {M(profit)} · outstanding {M(outstanding)}";
    }

    protected override Task<IReadOnlyList<SalesRegisterReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LoadAsync(context, cancellationToken);
    }

    /// <summary>One animal sale: profit is the price less the animal's cost to date, and nothing else.</summary>
    public static SalesRegisterReportRow AnimalSaleRow(
        string section,
        Animal animal,
        string breedName,
        decimal costToDateBdt,
        TradeBill? bill,
        ReportLanguage language)
    {
        ArgumentNullException.ThrowIfNull(animal);

        var price = animal.SalePriceBdt ?? 0m;
        var weight = animal.SaleWeightKg is > 0m ? animal.SaleWeightKg : null;

        return new SalesRegisterReportRow(
            section,
            animal.SaleDate ?? DateOnly.MinValue,
            animal.Tag.TagId,
            breedName,
            string.IsNullOrWhiteSpace(animal.BuyerName) ? "—" : animal.BuyerName.Trim(),
            weight,
            price,
            weight is { } kg ? decimal.Round(price / kg, 2, MidpointRounding.AwayFromZero) : null,
            costToDateBdt,
            price - costToDateBdt,
            Terms(bill, language),
            Outstanding(bill));
    }

    private Task<IReadOnlyList<SalesRegisterReportRow>> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync<IReadOnlyList<SalesRegisterReportRow>>("sales-register", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var bn = context.Language == ReportLanguage.Bn;
            var language = context.Language;

            var receivables = await _bills
                .GetByFarmIdAsync(farmId, TradeBillKind.Receivable, includeCancelled: false, cancellationToken)
                .ConfigureAwait(false);
            var billBySource = receivables
                .GroupBy(b => (b.SourceDocumentId, b.Category))
                .ToDictionary(g => g.Key, g => g.First());

            // ── Animal sales ────────────────────────────────────────────────
            var sold = (await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false))
                .Where(a => a.Status == AnimalStatus.Sold && a.SaleDate is { } d && period.Contains(d))
                .OrderBy(a => a.SaleDate)
                .ThenBy(a => a.Tag.TagId, StringComparer.Ordinal)
                .ToList();

            var rows = new List<SalesRegisterReportRow>(sold.Count);
            var animalSection = bn ? "১. পশু বিক্রয়" : "1. Animal sales";

            if (sold.Count > 0)
            {
                var breeds = (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false))
                    .ToDictionary(b => b.Id, b => b.Name);
                var feed = (await _feed.GetTotalsForFarmAsync(farmId, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken).ConfigureAwait(false))
                    .ToDictionary(t => t.AnimalId);
                var ledgers = (await _ledgers.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false))
                    .GroupBy(l => l.AnimalId)
                    .ToDictionary(g => g.Key, g => g.First());

                foreach (var animal in sold)
                {
                    ledgers.TryGetValue(animal.Id, out var ledger);
                    var feedCost = feed.TryGetValue(animal.Id, out var f) ? f.TotalCostBdt : 0m;
                    var cost = _valuation.CostToDate(animal, ledger, feedCost).TotalBdt;
                    billBySource.TryGetValue((animal.Id, TransactionCategory.AnimalSale), out var bill);

                    rows.Add(AnimalSaleRow(
                        animalSection,
                        animal,
                        breeds.TryGetValue(animal.BreedId, out var breed) ? breed : "—",
                        cost,
                        bill,
                        language));
                }
            }

            // ── Other sales, straight from the books ───────────────────────
            if (context.Bool("includeOtherSales", true))
            {
                var ledger = await _engine.LoadAsync(farmId, period.To, cancellationToken).ConfigureAwait(false);
                var otherSection = bn ? "২. অন্যান্য বিক্রয় (দুধ, উপজাত, অন্যান্য আয়)" : "2. Other sales (milk, by-products, other income)";

                foreach (var t in ledger.PostingsBetween(period.From, period.To)
                             .Where(t => AccountChart.IsRevenue(t) && t.Category != TransactionCategory.AnimalSale))
                {
                    var head = AccountChart.For(t.Category);
                    TradeBill? bill = null;
                    if (t.SourceDocumentId is { } source)
                    {
                        billBySource.TryGetValue((source, t.Category), out bill);
                    }

                    rows.Add(new SalesRegisterReportRow(
                        otherSection,
                        DateOnly.FromDateTime(t.TransactionDate),
                        AccountChart.VoucherNo(t),
                        bn ? head.NameBn : head.Name,
                        bill?.PartyName ?? AccountChart.Describe(t),
                        null,
                        AccountChart.ProfitEffect(t),
                        null,
                        null,
                        null,
                        Terms(bill, language, credit: t.Settlement == SettlementAccount.Receivable),
                        Outstanding(bill)));
                }
            }

            return rows;
        });

    private static string Terms(TradeBill? bill, ReportLanguage language, bool credit = false)
    {
        var bn = language == ReportLanguage.Bn;
        return bill is not null || credit ? (bn ? "বাকি" : "Credit") : (bn ? "নগদ" : "Cash");
    }

    private static decimal Outstanding(TradeBill? bill) =>
        bill is { IsCancelled: false } ? Math.Max(0m, bill.OutstandingBdt) : 0m;
}
