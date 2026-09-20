using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Finance.Queries;
using Farm360.Application.Livestock.Queries;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Reporting.Definitions.Finance;

public sealed record AnimalCostLedgerReportRow(
    string CostCategory,
    string Description,
    decimal AmountBdt);

/// <summary>
/// Report FN5 — Comprehensive cost accumulation and unit economics ledger for an individual animal.
/// </summary>
public sealed class AnimalCostLedgerReportDefinition : ReportDefinition<AnimalCostLedgerReportRow>
{
    public override string Key => "finance.animal-cost-ledger";

    public override LocalizedText Title => new("Animal Cost Ledger Report", "পশুর খরচ খতিয়ান প্রতিবেদন");

    public override LocalizedText Description => new(
        "Unit economics and life-to-date cost accumulation by category for an individual animal.",
        "একটি নির্দিষ্ট পশুর জন্য খাতভিত্তিক সামগ্রিক খরচ খতিয়ান ও অর্থনৈতিক হিসাব প্রতিবেদন।");

    public override ReportCategory Category => ReportCategory.Finance;

    public override PageSetup Page => PageSetup.A4PortraitSigned;

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
        var animalId = context.Id("animalId");
        var animal = await context.Send(new GetAnimalByIdQuery(animalId), cancellationToken).ConfigureAwait(false);
        return animal is null
            ? null
            : $"Tag {animal.TagId} · {animal.Species} · {animal.Sex} · {animal.Status}";
    }

    protected override async Task<IReadOnlyList<AnimalCostLedgerReportRow>> FetchAsync(
        ReportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var animalId = context.Id("animalId");

        var ledger = await context
            .Send(new GetAnimalCostLedgerQuery(animalId), cancellationToken)
            .ConfigureAwait(false);

        if (ledger is null)
        {
            return [];
        }

        return
        [
            new AnimalCostLedgerReportRow("Acquisition", "Purchase or initial capitalization valuation", ledger.AcquisitionCostBdt),
            new AnimalCostLedgerReportRow("Feed & Nutrition", "Direct ration consumption and allocated feed costs", ledger.TotalFeedCostBdt),
            new AnimalCostLedgerReportRow("Veterinary & Health", "Medications, vaccines, treatments, and vet procedures", ledger.TotalVetCostBdt),
            new AnimalCostLedgerReportRow("Labour Allocation", "Apportioned barn management and farm labor expenses", ledger.TotalLaborCostBdt),
            new AnimalCostLedgerReportRow("Overhead Allocation", "Barn utilities, facility depreciation, and administration", ledger.TotalOverheadBdt),
        ];
    }
}
