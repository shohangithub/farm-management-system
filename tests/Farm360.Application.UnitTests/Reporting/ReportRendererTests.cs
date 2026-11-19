using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Farm360.Application.Reporting.Model;
using Farm360.Infrastructure.Reporting.Csv;
using Farm360.Infrastructure.Reporting.Excel;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Reporting;

public class ReportRendererTests
{
    private static Task<ReportDataSet> GroupedDataAsync(ReportLanguage language = ReportLanguage.En) =>
        ReportingTestKit.RunAsync(
            new StockTestReport(
            [
                new("Feed", "=HYPERLINK(\"http://x\")", "kg", 10m, 50m, 500m, 0.5m),
                new("Feed", "Bran", "kg", 5m, 40m, 200m, 0.5m),
                new("Vet", "-Vaccine", "dose", 40m, 25m, 1000m, 1m),
            ]),
            ReportingTestKit.Context(language: language));

    [Fact]
    public async Task Excel_emits_group_bands_and_subtotal_formulas_with_numeric_cells()
    {
        var data = await GroupedDataAsync();
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(data);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.First();
        var used = sheet.RangeUsed()!.Rows().ToList();

        var groupRow = used.First(r => r.Cell(1).GetString() == "Section Feed");
        groupRow.Cell(1).Style.Font.Bold.Should().BeTrue();

        var subtotal = used.First(r => r.Cell(1).GetString().StartsWith("Sub-total — Section Feed", System.StringComparison.Ordinal));
        subtotal.Cell(1).Style.Font.Bold.Should().BeTrue();
        subtotal.Cell(4).FormulaA1.Should().StartWith("SUBTOTAL(9,", "money totals stay live formulas");
        subtotal.Cell(2).FormulaA1.Should().StartWith("SUBTOTAL(9,", "a single-unit quantity is summable");

        var grand = used.Last();
        grand.Cell(4).FormulaA1.Should().StartWith("SUBTOTAL(9,");
        grand.Cell(2).FormulaA1.Should().BeEmpty("kg and doses cannot be totalled");

        var detail = used.First(r => r.Cell(1).GetString() == "Bran");
        detail.Cell(4).DataType.Should().Be(XLDataType.Number);
        detail.Cell(4).GetValue<decimal>().Should().Be(200m);

        var formulaText = used.First(r => r.Cell(1).GetString().StartsWith("=HYPERLINK", System.StringComparison.Ordinal));
        formulaText.Cell(1).HasFormula.Should().BeFalse("user text is written as text, never as a formula");
    }

    [Fact]
    public async Task Excel_wide_reports_print_landscape_one_page_wide()
    {
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(await GroupedDataAsync());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var setup = workbook.Worksheets.First().PageSetup;
        setup.PagesWide.Should().Be(1);
    }

    [Fact]
    public async Task Excel_headers_follow_the_report_language()
    {
        var bytes = await new ClosedXmlReportRenderer().RenderAsync(await GroupedDataAsync(ReportLanguage.Bn));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        workbook.Worksheets.First().RangeUsed()!.Cells().Select(c => c.GetString())
            .Should().Contain(["ট্যাগ", "পরিমাণ"]);
    }

    [Fact]
    public async Task Csv_carries_a_group_column_and_subtotal_rows()
    {
        var csv = await RenderCsvAsync(await GroupedDataAsync());
        var lines = csv.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().StartWith("Group,Row type,Tag,Qty");
        lines.Should().Contain(l => l.StartsWith("Section Feed,Detail,Bran,5", System.StringComparison.Ordinal));
        lines.Should().Contain(l => l.StartsWith("Section Feed,Sub-total,,15", System.StringComparison.Ordinal));
        lines.Should().Contain(l => l.StartsWith(",Grand total,,,", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Csv_guards_against_formula_injection()
    {
        var csv = await RenderCsvAsync(await GroupedDataAsync());

        csv.Should().Contain("\"'=HYPERLINK(\"\"http://x\"\")\"");
        csv.Should().Contain(",'-Vaccine,");
        CsvReportRenderer.GuardText("@SUM(A1)").Should().Be("'@SUM(A1)");
        CsvReportRenderer.GuardText("+880").Should().Be("'+880");
        CsvReportRenderer.GuardText("BD-0042").Should().Be("BD-0042");
    }

    private static async Task<string> RenderCsvAsync(ReportDataSet data)
    {
        var bytes = await new CsvReportRenderer().RenderAsync(data);
        return Encoding.UTF8.GetString(bytes).TrimStart('﻿');
    }
}
