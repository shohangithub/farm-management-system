using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One purchase line. Quantity is in <paramref name="Unit"/>; totals add it up only within one unit.</summary>
public sealed record PurchaseRegisterReportRow(
    string Section,
    DateOnly Date,
    string Reference,
    string Supplier,
    string Item,
    decimal? Quantity,
    string Unit,
    decimal? UnitCostBdt,
    decimal AmountBdt,
    decimal PaidBdt,
    decimal OutstandingBdt);

/// <summary>
/// Report FN13 — purchase register: animals bought, feed and other stock received (purchase orders
/// and direct stock-in) and fixed assets bought in the period, with supplier, quantity and unit, unit
/// cost, amount, and how much is paid and still owed; returns to suppliers are shown as negatives.
/// </summary>
/// <remarks>
/// <para>
/// The register is driven by the ledger postings for the period (Livestock, Stock and Fixed Asset
/// purchases and stock returns from the financial statement engine), so each section's amount equals
/// what the books capitalised. The posting's source document then supplies the detail: the stock
/// transaction or purchase-order lines, the animal, or the asset.
/// </para>
/// <para>
/// Paid / outstanding come from the supplier bill raised for a credit purchase (today's balance; a
/// purchase order's balance is shared across its lines by value). Stock brought in by the owner
/// without payment is capital, not a purchase, and is not listed.
/// </para>
/// </remarks>
public sealed class PurchaseRegisterReportDefinition : ReportDefinition<PurchaseRegisterReportRow>
{
    private static readonly TransactionCategory[] PurchaseCategories =
    [
        TransactionCategory.AnimalPurchase,
        TransactionCategory.InventoryPurchase,
        TransactionCategory.InventoryPurchaseReturn,
        TransactionCategory.FixedAssetPurchase,
    ];

    private readonly IFinancialStatementEngine _engine;
    private readonly IStockTransactionRepository _stock;
    private readonly IPurchaseOrderRepository _orders;
    private readonly IInventoryItemRepository _items;
    private readonly ISupplierRepository _suppliers;
    private readonly ITradeBillRepository _bills;
    private readonly IAnimalRepository _animals;
    private readonly IBreedRepository _breeds;
    private readonly IFixedAssetRepository _assets;

    public PurchaseRegisterReportDefinition(
        IFinancialStatementEngine engine,
        IStockTransactionRepository stock,
        IPurchaseOrderRepository orders,
        IInventoryItemRepository items,
        ISupplierRepository suppliers,
        ITradeBillRepository bills,
        IAnimalRepository animals,
        IBreedRepository breeds,
        IFixedAssetRepository assets)
    {
        _engine = engine;
        _stock = stock;
        _orders = orders;
        _items = items;
        _suppliers = suppliers;
        _bills = bills;
        _animals = animals;
        _breeds = breeds;
        _assets = assets;
    }

    public override string Key => "finance.purchase-register";

    public override LocalizedText Title => new("Purchase Register", "ক্রয় রেজিস্টার");

    public override LocalizedText Description => new(
        "Animals, feed and other stock (purchase orders and stock-in) and fixed assets bought in the period, with supplier, quantity, unit cost, amount, paid and outstanding.",
        "নির্বাচিত সময়ে কেনা পশু, খাদ্য ও অন্যান্য মজুদ (ক্রয় আদেশ ও মজুদ গ্রহণ) এবং স্থায়ী সম্পদ — সরবরাহকারী, পরিমাণ, একক দর, মূল্য, পরিশোধিত ও বকেয়া।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4LandscapeSigned;

    public override LocalizedText? GrandTotalLabel => new("Total purchases", "মোট ক্রয়");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Farm(required: true),
        ReportParameter.DateRange("period", @default: "current-month"),
    ];

    public override IReadOnlyList<ReportColumn<PurchaseRegisterReportRow>> Columns =>
    [
        ReportColumn<PurchaseRegisterReportRow>.Date("date", new LocalizedText("Date", "তারিখ"), r => r.Date),
        ReportColumn<PurchaseRegisterReportRow>.Text("ref", new LocalizedText("Reference", "রেফারেন্স"), r => r.Reference, width: 1.2f),
        ReportColumn<PurchaseRegisterReportRow>.Text("supplier", new LocalizedText("Supplier", "সরবরাহকারী"), r => r.Supplier, width: 1.6f),
        ReportColumn<PurchaseRegisterReportRow>.Text("item", new LocalizedText("Item / Animal", "পণ্য / পশু"), r => r.Item, width: 2f),
        ReportColumn<PurchaseRegisterReportRow>.Number("qty", new LocalizedText("Quantity", "পরিমাণ"), r => r.Quantity, 2, 20f, ReportAggregate.Sum, r => r.Unit),
        ReportColumn<PurchaseRegisterReportRow>.Text("unit", new LocalizedText("Unit", "একক"), r => r.Unit, width: 14f, relative: false),
        ReportColumn<PurchaseRegisterReportRow>.Money("unitCost", new LocalizedText("Unit cost", "একক দর"), r => r.UnitCostBdt, widthMm: 22f),
        ReportColumn<PurchaseRegisterReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "মূল্য (টাকা)"), r => r.AmountBdt, widthMm: 26f, aggregate: ReportAggregate.Sum),
        ReportColumn<PurchaseRegisterReportRow>.Money("paid", new LocalizedText("Paid", "পরিশোধিত"), r => r.PaidBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
        ReportColumn<PurchaseRegisterReportRow>.Money("outstanding", new LocalizedText("Outstanding (today)", "বকেয়া (আজ)"), r => r.OutstandingBdt, widthMm: 24f, aggregate: ReportAggregate.Sum),
    ];

    public override ReportGroup<PurchaseRegisterReportRow>? Group =>
        ReportGroup<PurchaseRegisterReportRow>.By(r => r.Section, r => r.Section);

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rows = await LoadAsync(context, cancellationToken).ConfigureAwait(false);
        string M(decimal v) => ReportValueFormatter.Format(v, ReportColumnType.Money, 2, context.BengaliNumerals);
        var total = rows.Sum(r => r.AmountBdt);
        var owed = rows.Sum(r => r.OutstandingBdt);

        return context.Language == ReportLanguage.Bn
            ? $"মোট ক্রয় {M(total)} টাকা · পরিশোধিত {M(total - owed)} · বকেয়া {M(owed)}"
            : $"Purchases {M(total)} BDT · paid {M(total - owed)} · outstanding {M(owed)}";
    }

    protected override Task<IReadOnlyList<PurchaseRegisterReportRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return LoadAsync(context, cancellationToken);
    }

    private Task<IReadOnlyList<PurchaseRegisterReportRow>> LoadAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync<IReadOnlyList<PurchaseRegisterReportRow>>("purchase-register", async () =>
        {
            var farmId = context.FarmId();
            var period = context.Range("period");
            var bn = context.Language == ReportLanguage.Bn;
            string T(string en, string bnText) => bn ? bnText : en;

            var ledger = await _engine.LoadAsync(farmId, period.To, cancellationToken).ConfigureAwait(false);
            var postings = ledger.PostingsBetween(period.From, period.To)
                .Where(t => PurchaseCategories.Contains(t.Category) && t.AmountBdt != 0m)
                .ToList();

            if (postings.Count == 0)
            {
                return [];
            }

            var bills = (await _bills.GetByFarmIdAsync(farmId, TradeBillKind.Payable, includeCancelled: false, cancellationToken).ConfigureAwait(false))
                .GroupBy(b => (b.SourceDocumentId, b.Category))
                .ToDictionary(g => g.Key, g => g.First());
            var suppliers = (await _suppliers.GetAllAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(s => s.Id, s => s.Name);
            var items = (await _items.GetByFarmIdAsync(farmId, null, cancellationToken).ConfigureAwait(false))
                .ToDictionary(i => i.Id);
            var stock = (await _stock.GetByFarmIdAsync(farmId, period.From, period.To, cancellationToken).ConfigureAwait(false))
                .ToDictionary(s => s.Id);
            var animals = postings.Any(p => p.Category == TransactionCategory.AnimalPurchase)
                ? (await _animals.GetAllByFarmAsync(farmId, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.Id)
                : new Dictionary<Guid, Farm360.Domain.Livestock.Animal>();
            var breeds = animals.Count > 0
                ? (await _breeds.GetAllAsync(cancellationToken).ConfigureAwait(false)).ToDictionary(b => b.Id, b => b.Name)
                : new Dictionary<Guid, string>();
            var assets = postings.Any(p => p.Category == TransactionCategory.FixedAssetPurchase)
                ? (await _assets.GetByFarmIdAsync(farmId, cancellationToken).ConfigureAwait(false)).ToDictionary(a => a.Id)
                : new Dictionary<Guid, Farm360.Domain.Finance.Accounting.FixedAsset>();

            var livestock = T("1. Livestock purchases", "১. পশু ক্রয়");
            var feed = T("2. Feed purchases", "২. খাদ্য ক্রয়");
            var otherStock = T("3. Medicine, vaccine & other stock purchases", "৩. ঔষধ, টিকা ও অন্যান্য মজুদ ক্রয়");
            var capital = T("4. Capital (fixed asset) purchases", "৪. মূলধনী (স্থায়ী সম্পদ) ক্রয়");
            var returns = T("5. Returns to suppliers", "৫. সরবরাহকারীকে ফেরত");
            var head = T("head", "টি");
            var each = T("item", "টি");

            var rows = new List<PurchaseRegisterReportRow>();

            foreach (var t in postings)
            {
                var date = DateOnly.FromDateTime(t.TransactionDate);
                var amount = t.Type == TransactionType.Expense ? t.AmountBdt : -t.AmountBdt;
                var source = t.SourceDocumentId ?? t.AnimalId ?? Guid.Empty;
                bills.TryGetValue((source, t.Category), out var bill);
                var (paid, owed) = Settlement(t, bill, amount);
                var reference = AccountChart.VoucherNo(t);

                switch (t.Category)
                {
                    case TransactionCategory.AnimalPurchase:
                    {
                        var animalId = t.AnimalId ?? source;
                        var item = animals.TryGetValue(animalId, out var animal)
                            ? $"{animal.Tag.TagId} · {(breeds.TryGetValue(animal.BreedId, out var b) ? b : "—")}"
                            : AccountChart.Describe(t);
                        rows.Add(new(livestock, date, animal?.Tag.TagId ?? reference, bill?.PartyName ?? "—", item,
                            1m, head, amount, amount, paid, owed));
                        break;
                    }

                    case TransactionCategory.FixedAssetPurchase:
                    {
                        var item = assets.TryGetValue(source, out var asset) ? $"{asset.Code} · {asset.Name}" : AccountChart.Describe(t);
                        rows.Add(new(capital, date, reference, bill?.PartyName ?? "—", item, 1m, each, amount, amount, paid, owed));
                        break;
                    }

                    case TransactionCategory.InventoryPurchaseReturn:
                        rows.Add(new(returns, date, reference, bill?.PartyName ?? "—", AccountChart.Describe(t), null, string.Empty, null, amount, paid, owed));
                        break;

                    default:
                    {
                        if (stock.TryGetValue(source, out var stockIn))
                        {
                            items.TryGetValue(stockIn.InventoryItemId, out var item);
                            var qty = stockIn.EnteredQuantity is > 0m ? stockIn.EnteredQuantity.Value : stockIn.Quantity;
                            var unit = !string.IsNullOrWhiteSpace(stockIn.EnteredUnitName) ? stockIn.EnteredUnitName! : item?.UnitOfMeasure ?? string.Empty;
                            var supplier = bill?.PartyName
                                ?? (stockIn.SupplierId is { } sid && suppliers.TryGetValue(sid, out var sName) ? sName : "—");
                            rows.Add(new(
                                StockSection(item, feed, otherStock), date,
                                string.IsNullOrWhiteSpace(stockIn.InvoiceNumber) ? reference : stockIn.InvoiceNumber!,
                                supplier, item?.Name ?? AccountChart.Describe(t),
                                qty, unit, qty > 0m ? Round(amount / qty) : null, amount, paid, owed));
                            break;
                        }

                        var order = await _orders.GetByIdWithItemsAsync(source, cancellationToken).ConfigureAwait(false);
                        if (order is not null && order.FarmId == farmId && order.Items.Count > 0)
                        {
                            var supplier = bill?.PartyName ?? (suppliers.TryGetValue(order.SupplierId, out var sName) ? sName : "—");
                            rows.AddRange(OrderLines(order, items, amount, paid, owed, date, supplier, feed, otherStock));
                            break;
                        }

                        rows.Add(new(otherStock, date, reference, bill?.PartyName ?? "—", AccountChart.Describe(t), null, string.Empty, null, amount, paid, owed));
                        break;
                    }
                }
            }

            return rows
                .OrderBy(r => r.Section, StringComparer.Ordinal)
                .ThenBy(r => r.Date)
                .ThenBy(r => r.Reference, StringComparer.Ordinal)
                .ToList();
        });

    /// <summary>
    /// A purchase order's lines. The posting is the order total; line amounts are its share by value,
    /// with any rounding left on the last line, so the lines always add up to what the books hold.
    /// </summary>
    private static IEnumerable<PurchaseRegisterReportRow> OrderLines(
        PurchaseOrder order,
        Dictionary<Guid, InventoryItem> items,
        decimal amount,
        decimal paid,
        decimal owed,
        DateOnly date,
        string supplier,
        string feed,
        string otherStock)
    {
        var lines = order.Items.ToList();
        var basis = lines.Sum(l => l.TotalCostBdt);
        decimal usedAmount = 0m, usedPaid = 0m, usedOwed = 0m;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var last = i == lines.Count - 1;
            var share = basis > 0m ? line.TotalCostBdt / basis : 1m / lines.Count;
            var lineAmount = last ? amount - usedAmount : Round(amount * share);
            var linePaid = last ? paid - usedPaid : Round(paid * share);
            var lineOwed = last ? owed - usedOwed : Round(owed * share);
            usedAmount += lineAmount;
            usedPaid += linePaid;
            usedOwed += lineOwed;

            items.TryGetValue(line.InventoryItemId, out var item);
            var qty = line.EnteredQuantity is > 0m ? line.EnteredQuantity.Value : line.Quantity;
            var unit = !string.IsNullOrWhiteSpace(line.EnteredUnitName) ? line.EnteredUnitName! : item?.UnitOfMeasure ?? string.Empty;

            yield return new PurchaseRegisterReportRow(
                StockSection(item, feed, otherStock), date, order.PoNumber, supplier, item?.Name ?? "—",
                qty, unit, qty > 0m ? Round(lineAmount / qty) : null, lineAmount, linePaid, lineOwed);
        }
    }

    private static string StockSection(InventoryItem? item, string feed, string otherStock) =>
        item?.Category == InventoryCategory.Feed ? feed : otherStock;

    /// <summary>Paid and still owed: from the supplier bill for a credit purchase, otherwise paid in full.</summary>
    private static (decimal Paid, decimal Owed) Settlement(FinancialTransaction posting, TradeBill? bill, decimal amount)
    {
        if (posting.Settlement != SettlementAccount.Payable || bill is null || amount <= 0m)
        {
            return (amount, 0m);
        }

        var owed = Math.Clamp(bill.OutstandingBdt, 0m, amount);
        return (amount - owed, owed);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
