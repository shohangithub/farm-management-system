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

public sealed record AnimalCostLedgerReportRow(
    string CostCategory,
    string Description,
    decimal AmountBdt);

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

    public override LocalizedText Title => new("Animal Cost Ledger Report", "পশুর খরচ খতিয়ান প্রতিবেদন");

    public override LocalizedText Description => new(
        "Unit economics and life-to-date cost accumulation by category for an individual animal.",
        "একটি নির্দিষ্ট পশুর জন্য খাতভিত্তিক সামগ্রিক খরচ খতিয়ান ও অর্থনৈতিক হিসাব প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

    public override LocalizedText? GrandTotalLabel => new("Total cost to date", "এ পর্যন্ত মোট খরচ");

    public override IReadOnlyList<ReportParameter> Parameters =>
    [
        ReportParameter.Animal("animalId", required: true),
    ];

    public override IReadOnlyList<ReportColumn<AnimalCostLedgerReportRow>> Columns =>
    [
        ReportColumn<AnimalCostLedgerReportRow>.Text("category", new LocalizedText("Cost Category", "খরচের খাত"), r => r.CostCategory, width: 2.5f),
        ReportColumn<AnimalCostLedgerReportRow>.Text("description", new LocalizedText("Description / Basis", "বিবরণ / ভিত্তি"), r => r.Description, width: 3.5f),
        ReportColumn<AnimalCostLedgerReportRow>.Money("amount", new LocalizedText("Amount (BDT)", "পরিমাণ (টাকা)"), r => r.AmountBdt, decimals: 2, widthMm: 32f, aggregate: ReportAggregate.Sum),
    ];

    public override async Task<string?> ResolveSubjectAsync(ReportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var animal = await LoadAnimalAsync(context, cancellationToken).ConfigureAwait(false);
        return animal is null
            ? null
            : $"Tag {animal.Tag.TagId} · {animal.Species} · {animal.Sex} · {animal.Status}";
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
            ? T("Confirmed feed allocations (no cost ledger yet)", "নিশ্চিত খাদ্য বরাদ্দ (খরচ খতিয়ান এখনও নেই)")
            : T("Direct ration consumption and allocated feed costs", "সরাসরি খাদ্য গ্রহণ ও বরাদ্দকৃত খাদ্য খরচ");

        return
        [
            new(T("Acquisition", "ক্রয়"), T("Purchase or initial capitalization valuation", "ক্রয়মূল্য বা প্রারম্ভিক মূল্যায়ন"), cost.PurchaseBdt),
            new(T("Feed & Nutrition", "খাদ্য ও পুষ্টি"), feedBasis, cost.FeedBdt),
            new(T("Veterinary & Health", "চিকিৎসা ও স্বাস্থ্য"), T("Medications, vaccines, treatments, and vet procedures", "ওষুধ, টিকা, চিকিৎসা ও পশুচিকিৎসা"), cost.VetBdt),
            new(T("Labour Allocation", "শ্রম বণ্টন"), T("Apportioned barn management and farm labour", "বণ্টনকৃত খামার ব্যবস্থাপনা ও শ্রম"), cost.LabourBdt),
            new(T("Overhead Allocation", "পরোক্ষ খরচ বণ্টন"), T("Utilities, facility depreciation, and administration", "ইউটিলিটি, অবচয় ও প্রশাসনিক খরচ"), cost.OverheadBdt),
        ];
    }

    private Task<Domain.Livestock.Animal?> LoadAnimalAsync(ReportContext context, CancellationToken cancellationToken) =>
        context.MemoAsync("animal", () => _animals.GetByIdAsync(context.Id("animalId"), cancellationToken));
}
