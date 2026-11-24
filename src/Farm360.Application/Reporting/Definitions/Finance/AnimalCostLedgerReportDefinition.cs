using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Reporting.Definitions.Finance;

/// <summary>One kind of cost. <paramref name="ShareOfTotal"/> is a 0–1 ratio of the animal's total cost.</summary>
public sealed record AnimalCostLedgerReportRow(
    string CostCategory,
    string Description,
    decimal AmountBdt,
    decimal? ShareOfTotal = null);

/// <summary>
/// Report FN5 — Comprehensive cost accumulation and unit economics ledger for an individual animal.
/// </summary>
/// <remarks>
/// Costs come from <see cref="IAnimalValuationService.CostToDate"/>, the same source the Animal
/// Summary and Herd Summary reports use. An animal with no cost ledger yet (bought last week, or
/// recorded before the ledger existed) therefore shows its purchase price plus allocated feed here,
/// exactly as it does on the summaries — rather than an empty report that contradicts them.
/// </remarks>
public sealed class AnimalCostLedgerReportDefinition : ReportDefinition<AnimalCostLedgerReportRow>
{
    private readonly IAnimalRepository _animals;
    private readonly IAnimalCostLedgerRepository _ledgers;
    private readonly IAnimalFeedAllocationRepository _feed;
    private readonly IAnimalValuationService _valuation;

    public AnimalCostLedgerReportDefinition(
        IAnimalRepository animals,
        IAnimalCostLedgerRepository ledgers,
        IAnimalFeedAllocationRepository feed,
        IAnimalValuationService valuation)
    {
        _animals = animals;
        _ledgers = ledgers;
        _feed = feed;
        _valuation = valuation;
    }

    public override string Key => "finance.animal-cost-ledger";

    public override LocalizedText Title => new("Animal Cost to Date", "পশুর এ পর্যন্ত মোট খরচ");

    public override LocalizedText Description => new(
        "Everything one animal has cost since it came to the farm — purchase price, feed, vet & medicine, labour and overhead.",
        "খামারে আসার পর থেকে একটি পশুর পেছনে মোট কত খরচ হয়েছে — ক্রয়মূল্য, খাদ্য, চিকিৎসা ও ওষুধ, শ্রম ও পরোক্ষ খরচ।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override LocalizedText? GrandTotalLabel => new("Total cost to date", "এ পর্যন্ত মোট খরচ");

    public override IReadOnlyList<LocalizedText> Notes =>
    [
        new("Each line is one kind of cost; the total at the bottom is what the animal has cost the farm so far.",
            "প্রতিটি সারি এক ধরনের খরচ; নিচের মোট হলো পশুটির পেছনে এ পর্যন্ত খামারের মোট খরচ।"),
        new("Purchase price is what was paid for the animal, or its value when it was first recorded.",
            "ক্রয়মূল্য হলো পশুটি কেনার দাম, অথবা প্রথম নথিভুক্ত করার সময় ধরা মূল্য।"),
        new("Labour and overhead are farm-wide bills shared among animals by the days each was on the farm.",
            "শ্রম ও পরোক্ষ খরচ পুরো খামারের বিল, যা প্রতিটি পশুর খামারে থাকার দিন অনুযায়ী ভাগ করা হয়েছে।"),
        new("Share of total = this cost ÷ the animal's total cost. A sold or dead animal's costs stop the day before it left.",
            "মোটের অংশ = এই খরচ ÷ পশুটির মোট খরচ। বিক্রি বা মৃত পশুর খরচ খামার ছাড়ার আগের দিন পর্যন্ত ধরা হয়েছে।"),
    ];

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
    ];

    public override IReadOnlyList<ReportColumn<AnimalCostLedgerReportRow>> Columns =>
    [
        ReportColumn<AnimalCostLedgerReportRow>.Text("category", new LocalizedText("Type of cost", "খরচের ধরন"), r => r.CostCategory, width: 2.2f),
        ReportColumn<AnimalCostLedgerReportRow>.Text("description", new LocalizedText("What it includes", "এতে যা আছে"), r => r.Description, width: 3.8f),
        ReportColumn<AnimalCostLedgerReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f, aggregate: ReportAggregate.Sum),
        ReportColumn<AnimalCostLedgerReportRow>.Percent("share", new LocalizedText("Share of total (%)", "মোটের অংশ (%)"), r => r.ShareOfTotal, decimals: 1, widthMm: 24f),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        return animal is null ? null : AnimalCostReportText.AnimalLine(animal, context.Language);
    }

    protected override async Task<IReadOnlyList<AnimalCostLedgerReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        if (animal is null)
        {
            return [];
        }

        var ledger = await _ledgers.GetByAnimalIdAsync(animal.Id, cancellationToken).ConfigureAwait(false);
        var feed = await _feed
            .GetTotalsForAnimalAsync(animal.Id, DateOnly.MinValue, DateOnly.MaxValue, cancellationToken)
            .ConfigureAwait(false);

        var cost = _valuation.CostToDate(animal, ledger, feed.TotalCostBdt);
        var bn = context.Language == ReportLanguage.Bn;
        string T(string en, string bnText) => bn ? bnText : en;

        var feedBasis = ledger is null
            ? T("Feed recorded as given to this animal (cost book not started yet)", "এই পশুকে দেওয়া হিসেবে নথিভুক্ত খাদ্য (খরচের হিসাব এখনও শুরু হয়নি)")
            : T("Feed this animal was given, at stock cost", "এই পশুকে দেওয়া খাদ্য, মজুদের দামে");

        var total = cost.TotalBdt;
        decimal? Share(decimal amount) => total != 0m ? decimal.Round(amount / total, 4, MidpointRounding.AwayFromZero) : null;
        AnimalCostLedgerReportRow Line(string category, string description, decimal amount) =>
            new(category, description, amount, Share(amount));

        return
        [
            Line(T("Purchase price", "ক্রয়মূল্য"), T("Price paid, or value when first recorded on the farm", "কেনার দাম, বা খামারে প্রথম নথিভুক্তির সময়ের মূল্য"), cost.PurchaseBdt),
            Line(T("Feed", "খাদ্য"), feedBasis, cost.FeedBdt),
            Line(T("Vet & medicine", "চিকিৎসা ও ওষুধ"), T("Treatments, medicines, vaccines and vet visits", "চিকিৎসা, ওষুধ, টিকা ও পশুচিকিৎসকের খরচ"), cost.VetBdt),
            Line(T("Labour", "শ্রম"), T("Its share of farm wages", "খামারের মজুরিতে এই পশুর অংশ"), cost.LabourBdt),
            Line(T("Overhead", "পরোক্ষ খরচ"), T("Its share of electricity, water, transport, upkeep and other farm costs", "বিদ্যুৎ, পানি, পরিবহন, রক্ষণাবেক্ষণ ও অন্যান্য খামার খরচে এই পশুর অংশ"), cost.OverheadBdt),
        ];
    }

    private Task<Domain.Livestock.Animal?> LoadAnimalAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("animal", () => _animals.GetByIdAsync(context.Id("animalId"), cancellationToken));
}
