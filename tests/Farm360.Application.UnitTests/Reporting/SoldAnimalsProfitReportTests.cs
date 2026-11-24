using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Farm360.Application.Reporting.Abstractions;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Application.Reporting.Model;
using Farm360.Infrastructure.Reporting.Excel;
using Farm360.Infrastructure.Reporting.Pdf;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class SoldAnimalsProfitReportTests
{
    private static SoldAnimalProfitReportRow Row(
        string tag, decimal purchase, decimal feed, decimal vet, decimal labour, decimal price,
        int days = 100, decimal? weight = 300m, bool noFeed = false, bool noPrice = false, decimal owed = 0m)
    {
        var cost = purchase + feed + vet + labour;
        var profit = price - cost;
        return new SoldAnimalProfitReportRow(
            profit >= 0 ? "1" : "2", profit >= 0 ? "Sold at a profit" : "Sold at a loss",
            tag + " · Local", tag, null, new DateOnly(2026, 11, 1), days, weight, 60m,
            purchase, feed, vet, labour, cost, price, profit,
            price > 0 ? profit / price : null, days > 0 ? profit / days : null,
            "verdict", noFeed, noPrice, owed);
    }

    private static readonly IReadOnlyList<SoldAnimalProfitReportRow> Rows =
    [
        Row("B-01", 50_000m, 15_000m, 1_000m, 4_000m, 90_000m),            // +20,000
        Row("B-02", 60_000m, 20_000m, 2_000m, 3_000m, 80_000m, days: 400), // −5,000
        Row("B-03", 40_000m, 0m, 0m, 1_000m, 45_000m, noFeed: true, owed: 10_000m), // +4,000
    ];

    [Fact]
    public void Summary_gives_the_net_result_and_the_split_between_winners_and_losers()
    {
        var summary = SoldAnimalsProfitReportDefinition.Summarize(ReportingTestKit.Context(), Rows);

        var net = summary.Figures.Single(f => f.Label == "Net profit");
        net.Value.Should().Be("19,000 BDT");
        net.Tone.Should().Be(ReportTone.Good);
        summary.Figures[0].Value.Should().Contain("3").And.Contain("2 profit").And.Contain("1 loss");
        summary.Figures.Should().Contain(f => f.Label == "Buyers still owe" && f.Tone == ReportTone.Bad);
    }

    [Fact]
    public void Summary_names_the_best_and_worst_sale_the_break_even_gap_and_missing_records()
    {
        var insights = SoldAnimalsProfitReportDefinition.Summarize(ReportingTestKit.Context(), Rows).Insights;

        insights.Should().Contain(i => i.StartsWith("Best sale: B-01 made 20,000 BDT", StringComparison.Ordinal));
        insights.Should().Contain(i => i.StartsWith("Biggest loss: B-02 lost 5,000 BDT", StringComparison.Ordinal));
        insights.Should().Contain(i => i.Contains("needed 5,000 BDT more", StringComparison.Ordinal));
        insights.Should().Contain(i => i.Contains("no feed cost recorded (B-03)", StringComparison.Ordinal));
        insights.Should().Contain(i => i.StartsWith("Where the money went:", StringComparison.Ordinal));
    }

    [Fact]
    public void Summary_reports_a_net_loss_as_bad_news()
    {
        var summary = SoldAnimalsProfitReportDefinition.Summarize(
            ReportingTestKit.Context(),
            [Row("C-01", 70_000m, 10_000m, 0m, 0m, 75_000m)]);

        summary.Figures.Single(f => f.Label == "Net loss").Tone.Should().Be(ReportTone.Bad);
        summary.Insights[0].Should().StartWith("Overall the 1 animals lost 5,000 BDT");
    }

    [Fact]
    public async Task Summary_box_reaches_the_dataset_and_renders_in_excel_and_pdf()
    {
        var data = await ReportingTestKit.RunAsync(new SummaryTestReport());
        data.Summary.Should().NotBeNull();

        var xlsx = await new ClosedXmlReportRenderer().RenderAsync(data);
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var cells = workbook.Worksheets.First().CellsUsed().Select(c => c.GetString()).ToList();
        cells.Should().Contain("Summary").And.Contain("Net profit").And.Contain("• All good");

        var pdf = await new QuestPdfReportRenderer().RenderAsync(data);
        pdf.Length.Should().BeGreaterThan(1000);
    }

    private sealed class SummaryTestReport : ReportDefinition<StockRow>
    {
        public override string Key => "test.summary";

        public override LocalizedText Title => new("Summary test", "সারসংক্ষেপ");

        public override ReportCategory Category => ReportCategory.Finance;

        public override IReadOnlyList<ReportColumn<StockRow>> Columns =>
        [
            ReportColumn<StockRow>.Text("tag", new LocalizedText("Tag", "ট্যাগ"), r => r.Tag),
            ReportColumn<StockRow>.Money("value", new LocalizedText("Value", "মূল্য"), r => r.Value, aggregate: ReportAggregate.Sum),
        ];

        protected override Task<IReadOnlyList<StockRow>> FetchAsync(ReportContext context, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockRow>>([new("A", "Bran", "kg", 1m, 1m, 10m, 1m)]);

        protected override Task<ReportSummary?> SummarizeAsync(ReportContext context, IReadOnlyList<StockRow> rows, CancellationToken cancellationToken) =>
            Task.FromResult<ReportSummary?>(new ReportSummary(
                "Summary",
                [new("Net profit", "10 BDT", ReportTone.Good), new("Animals sold", "1")],
                "What it tells us",
                ["All good"]));
    }
}
