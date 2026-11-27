using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Farm360.Application.Intelligence.SellOrKeep;
using Farm360.Application.Intelligence.Services;
using Farm360.Application.Reporting.Definitions.Livestock;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using Farm360.Infrastructure.Reporting.Excel;
using Farm360.Infrastructure.Reporting.Pdf;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class SellOrKeepReportTests
{
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly Guid Farm = Guid.NewGuid();

    private static readonly SellOrKeepAssumptions HandCase = new()
    {
        GrowthDeclineFraction = 0.5m,
        GrowthDeclineDays = 100,
        FeedScalingExponent = 0,
    };

    private static SellOrKeepAdvice Advise(string tag, decimal invested, decimal price, int treatments = 0, int weighings = 3, decimal adgKg = 1m) =>
        SellOrKeepCalculator.Advise(new SellOrKeepInput
        {
            AnimalId = Guid.NewGuid(),
            TagId = tag,
            BreedName = "Sahiwal Cross",
            AcquisitionDate = Today.AddDays(-100),
            Today = Today,
            Weighings = Enumerable.Range(0, weighings).Select(i => new SellOrKeepWeighing(Today.AddDays(-20 * (weighings - 1 - i)), 200m - adgKg * 20 * (weighings - 1 - i))).ToList(),
            Cost = new AnimalCostBreakdown(invested, 5_000m, 500m, 0m, 0m),
            FeedCostInWindowBdt = 301m * 30m,
            TreatmentsInWindow = treatments,
            BreedTargetAdgKg = 0.9m,
            Price = new SellOrKeepPrice(price, SellOrKeepPriceSource.OwnSales, 3),
        }, HandCase);

    private static IReadOnlyList<SellOrKeepAdvice> Herd() =>
    [
        Advise("K-01", 53_000m, 400m),                      // keep ~49 days
        Advise("S-01", 53_000m, 400m, treatments: 3),       // often sick, in profit → sell now
        Advise("L-01", 80_000m, 300m, adgKg: 0.5m),         // losing money
        Advise("W-01", 50_000m, 400m, weighings: 1),        // weigh first
    ];

    private static SellOrKeepReportDefinition Report(IReadOnlyList<SellOrKeepAdvice> advice)
    {
        var advisor = Substitute.For<IAnimalSellOrKeepAdvisor>();
        advisor.Assumptions.Returns(SellOrKeepAssumptions.Default);
        advisor.AdviseFarmAsync(Farm, Arg.Any<DateOnly?>(), Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(advice);

        var animals = Substitute.For<IAnimalRepository>();
        animals.GetWeighInsByFarmAsync(Farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(advice.SelectMany(a => new[] { (a.AnimalId, Today.AddDays(-20), 180m), (a.AnimalId, Today, 200m) }).ToList());
        var feed = Substitute.For<IAnimalFeedAllocationRepository>();
        feed.GetDailyEatenByFarmAsync(Farm, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(advice.Select(a => new AnimalFeedDay(a.AnimalId, Today.AddDays(-3), 10m, 300m)).ToList());
        var overheads = Substitute.For<IAnimalOverheadAllocationRepository>();
        overheads.GetByAnimalAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AnimalOverheadAllocation>());
        var treatments = Substitute.For<IMedicalTreatmentRepository>();
        treatments.GetByAnimalIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<MedicalTreatment>());

        return new SellOrKeepReportDefinition(advisor, animals, feed, overheads, treatments);
    }

    private static Dictionary<string, string?> Params(Guid? animalId = null) => new()
    {
        ["farmId"] = Farm.ToString(),
        ["animalId"] = animalId?.ToString(),
    };

    [Fact]
    public void Herd_rows_are_grouped_by_verdict_with_sell_now_first()
    {
        var rows = SellOrKeepReportDefinition.HerdRows(Herd(), ReportLanguage.En);

        rows.Select(r => r.GroupLabel).Distinct().Should().Equal("1. Sell now", "2. Keep for now", "3. Losing money — check feed or health", "4. Weigh it first");
        rows.Single(r => r.Item.StartsWith("S-01", StringComparison.Ordinal)).Advice.Should().Be("Sell now · often sick");
        rows.Single(r => r.Item.StartsWith("K-01", StringComparison.Ordinal)).DaysLeft.Should().Be(49);
        rows.Single(r => r.Item.StartsWith("L-01", StringComparison.Ordinal)).Advice.Should().StartWith("Losing money — slow growth");
    }

    [Fact]
    public void Herd_summary_counts_the_advice_and_the_extra_profit()
    {
        var summary = SellOrKeepReportDefinition.SummarizeHerd(ReportingTestKit.Context(today: Today), Herd());

        summary.Figures.Single(f => f.Label == "Sell now").Value.Should().Be("1");
        summary.Figures.Single(f => f.Label == "Losing money").Tone.Should().Be(ReportTone.Bad);
        summary.Figures.Single(f => f.Label == "Extra profit by following the advice").Value.Should().Be("2,401 BDT");
        summary.Insights.Should().Contain(i => i.StartsWith("Sell now: 1 animal(s) — S-01", StringComparison.Ordinal));
        summary.Insights.Should().Contain(i => i.StartsWith("Losing money: 1 animal(s)", StringComparison.Ordinal) && i.Contains("L-01: slow growth", StringComparison.Ordinal));
        summary.Insights.Should().Contain(i => i.StartsWith("Weigh first: W-01", StringComparison.Ordinal));
        summary.Insights.Should().Contain(i => i.Contains("based on your own sales of 3 Sahiwal Cross", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ReportLanguage.En, "Sell or Keep? Animal Decision Report", "Advice", "1. Sell now")]
    [InlineData(ReportLanguage.Bn, "রাখবেন না বিক্রি করবেন? পশুভিত্তিক সিদ্ধান্ত", "পরামর্শ", "১. এখনই বিক্রি করুন")]
    public async Task Herd_report_renders_to_excel_and_pdf_in_both_languages(ReportLanguage language, string title, string adviceHeader, string firstGroup)
    {
        var data = await ReportingTestKit.RunAsync(Report(Herd()), ReportingTestKit.Context(Params(), language, today: Today));

        data.Title.Should().Be(title);
        data.Summary.Should().NotBeNull();
        data.Notes.Should().HaveCount(8);
        data.Groups[0].GroupLabel.Should().Be(firstGroup);

        var xlsx = await new ClosedXmlReportRenderer().RenderAsync(data);
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var cells = workbook.Worksheets.First().CellsUsed().Select(c => c.GetString()).ToList();
        cells.Should().Contain(adviceHeader).And.Contain(firstGroup);

        var pdf = await new QuestPdfReportRenderer().RenderAsync(data);
        pdf.Length.Should().BeGreaterThan(1000);
    }

    [Theory]
    [InlineData(ReportLanguage.En, "1. Projection (every 15 days)", "Sell or keep?")]
    [InlineData(ReportLanguage.Bn, "১. ভবিষ্যৎ হিসাব (প্রতি 15 দিন)", "রাখবেন না বিক্রি করবেন?")]
    public async Task Single_animal_mode_shows_projection_and_history(ReportLanguage language, string projectionGroup, string summaryTitle)
    {
        var one = Advise("K-01", 53_000m, 400m);
        var data = await ReportingTestKit.RunAsync(Report([one]), ReportingTestKit.Context(Params(one.AnimalId), language, today: Today));

        data.Summary!.Title.Should().Be(summaryTitle);
        data.Groups.Select(g => g.GroupLabel).First().Should().Be(projectionGroup);
        data.Groups.Should().HaveCountGreaterThanOrEqualTo(3, "projection, weighings and monthly costs");
        // Every 15 days from 0 to 240 (17 lines) plus the best day (49).
        data.Groups[0].RowCount.Should().Be(18);

        var pdf = await new QuestPdfReportRenderer().RenderAsync(data);
        pdf.Length.Should().BeGreaterThan(1000);
        var xlsx = await new ClosedXmlReportRenderer().RenderAsync(data);
        xlsx.Length.Should().BeGreaterThan(1000);
    }
}
