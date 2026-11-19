using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Farm360.Application.Reporting.Definitions.Finance;
using Farm360.Application.Reporting.Model;
using Farm360.Application.Reporting.Services;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class ReportTotalsTests
{
    private static readonly StockRow[] SameUnit =
    [
        new("A", "BD-0042", "kg", 10m, 50m, 500m, 0.25m),
        new("A", "BD-0043", "kg", 30m, 50m, 1500m, 0.75m),
    ];

    [Fact]
    public async Task Quantity_is_totalled_when_every_row_shares_one_unit()
    {
        var data = await ReportingTestKit.RunAsync(new StockTestReport(SameUnit));

        data.GrandTotals[1].V.Should().Be(40m);
        data.Groups.Single().Totals[1].V.Should().Be(40m);
    }

    [Fact]
    public async Task Quantity_is_not_totalled_across_mixed_units()
    {
        StockRow[] rows =
        [
            new("A", "Urea", "kg", 10m, 50m, 500m, 0.5m),
            new("A", "Vaccine", "dose", 40m, 25m, 1000m, 0.5m),
        ];

        var data = await ReportingTestKit.RunAsync(new StockTestReport(rows));

        data.GrandTotals[1].V.Should().BeNull("12 kg + 40 doses is not 52 of anything");
        data.GrandTotals[3].V.Should().Be(1500m, "money is always in BDT and still adds up");
    }

    [Fact]
    public async Task Mixed_units_still_total_inside_a_single_unit_group()
    {
        StockRow[] rows =
        [
            new("Feed", "Urea", "kg", 10m, 50m, 500m, 0.5m),
            new("Feed", "Bran", "kg", 5m, 40m, 200m, 0.5m),
            new("Vet", "Vaccine", "dose", 40m, 25m, 1000m, 1m),
        ];

        var data = await ReportingTestKit.RunAsync(new StockTestReport(rows));

        data.Groups.Single(g => g.GroupKey == "Feed").Totals[1].V.Should().Be(15m);
        data.Groups.Single(g => g.GroupKey == "Vet").Totals[1].V.Should().Be(40m);
        data.GrandTotals[1].V.Should().BeNull();
    }

    [Fact]
    public async Task Money_rates_and_percentages_are_never_summed_by_default()
    {
        var data = await ReportingTestKit.RunAsync(new StockTestReport(SameUnit));

        data.Columns[2].Aggregate.Should().Be(ReportAggregate.None, "a rate column must opt in to a total, never default to one");
        data.GrandTotals[2].V.Should().BeNull();
        data.GrandTotals[4].V.Should().BeNull();
    }

    [Fact]
    public async Task Row_limit_is_enforced_before_cells_are_built()
    {
        var context = ReportingTestKit.Context(maxRows: 1);

        var act = () => ReportingTestKit.RunAsync(new StockTestReport(SameUnit), context);

        await act.Should().ThrowAsync<ReportTooLargeException>();
    }

    [Fact]
    public async Task Group_labels_and_dataset_language_follow_the_report_language()
    {
        var data = await ReportingTestKit.RunAsync(
            new StockTestReport(SameUnit),
            ReportingTestKit.Context(language: ReportLanguage.Bn));

        data.Groups.Single().GroupLabel.Should().Be("বিভাগ A");
        data.Language.Should().Be(ReportLanguage.Bn);
        data.GrandTotalCaption.Should().Be("সর্বমোট");
    }

    [Fact]
    public void Monthly_pnl_signs_expenses_so_the_total_is_the_net_result()
    {
        var rows = MonthlyPnlReportDefinition.BuildRows(
            new Dictionary<string, decimal> { ["AnimalSales"] = 100_000m, ["MilkSales"] = 20_000m },
            new Dictionary<string, decimal> { ["FeedCost"] = 70_000m, ["LabourCost"] = 15_000m },
            ReportLanguage.En);

        rows.Sum(r => r.AmountBdt).Should().Be(35_000m, "net = income 120,000 − expenses 85,000");
        rows.Where(r => r.Section.StartsWith("2.", StringComparison.Ordinal)).Should().OnlyContain(r => r.AmountBdt < 0);
        rows.Select(r => r.Category).Should().Contain("Animal Sales");
    }

    [Fact]
    public void Monthly_pnl_prints_an_explicit_net_line_instead_of_a_grand_total()
    {
        var definition = new MonthlyPnlReportDefinition();

        definition.GrandTotalLabel!.En.Should().Contain("Net profit");
        definition.Parameters.Single(p => p.Name == "year").DefaultValue
            .Should().Be(ReportTimeZone.Today(DateTime.UtcNow, ReportTimeZone.Default).Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "the default period is the current one, not a year frozen in the code");
    }

    [Fact]
    public void Batch_pnl_has_no_grand_total()
    {
        new BatchPnlReportDefinition().ShowGrandTotals.Should().BeFalse("revenue + cost + profit + ROI is not a number");
    }
}

public class BengaliNumeralTests
{
    [Fact]
    public void Text_cells_keep_their_digits()
    {
        ReportValueFormatter.Format("BD-0042", ReportColumnType.Text, 0, bengaliNumerals: true)
            .Should().Be("BD-0042");
    }

    [Theory]
    [InlineData(ReportColumnType.Money, "১,২৩৪.৫০")]
    [InlineData(ReportColumnType.Number, "১,২৩৪.৫০")]
    public void Numeric_cells_use_bengali_digits(ReportColumnType type, string expected)
    {
        ReportValueFormatter.Format(1234.5m, type, 2, bengaliNumerals: true).Should().Be(expected);
    }

    [Fact]
    public void Date_cells_use_bengali_digits()
    {
        ReportValueFormatter.Format(new DateOnly(2026, 11, 16), ReportColumnType.Date, 0, bengaliNumerals: true)
            .Should().Be("১৬-Nov-২০২৬");
    }

    [Fact]
    public async Task Dataset_converts_only_numeric_cells()
    {
        var data = await ReportingTestKit.RunAsync(
            new StockTestReport([new("A", "BD-0042", "kg", 10m, 50m, 500m, 0.25m)]),
            ReportingTestKit.Context(bengaliNumerals: true));

        var cells = data.Rows.Single().Cells;
        cells[0].F.Should().Be("BD-0042");
        cells[3].F.Should().Be("৫০০.০০");
    }

    [Fact]
    public void Boolean_cells_follow_the_report_language()
    {
        ReportValueFormatter.Format(true, ReportColumnType.Boolean, 0, false, ReportLanguage.Bn).Should().Be("হ্যাঁ");
    }
}
