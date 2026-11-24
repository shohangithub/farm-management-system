using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Farm360.Application.Documents;
using Farm360.Application.Reporting.Model;
using Farm360.Application.UnitTests.Reporting;
using Farm360.Infrastructure.Documents;
using Farm360.Infrastructure.Reporting.Excel;
using Farm360.Infrastructure.Reporting.Pdf;
using FluentAssertions;
using Xunit;

namespace Farm360.Application.UnitTests.Documents;

/// <summary>
/// Proves every customer document renders to a real PDF in English and in Bangla (with and
/// without Bengali numerals and a logo), the same way the report renderer tests do.
/// </summary>
public class CustomerDocumentRenderTests
{
    private readonly QuestPdfCustomerDocumentRenderer _renderer = new();

    public static TheoryData<ReportLanguage, bool, bool> Variants() => new()
    {
        { ReportLanguage.En, false, false },
        { ReportLanguage.En, false, true },
        { ReportLanguage.Bn, false, true },
        { ReportLanguage.Bn, true, false },
    };

    private static DocumentHeader Header(ReportLanguage language, bool bengaliNumerals, bool withLogo) =>
        DocumentTestKit.Header(language, bengaliNumerals, withLogo ? DocumentTestKit.TinyPng : null);

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 4 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F';

    [Theory]
    [MemberData(nameof(Variants))]
    public void Sale_invoice_renders(ReportLanguage language, bool bengaliNumerals, bool withLogo)
    {
        var header = Header(language, bengaliNumerals, withLogo);
        var doc = new SaleInvoiceDocument(
            header,
            "INV-0042",
            new DateOnly(2026, 11, 1),
            language == ReportLanguage.Bn ? "করিম ট্রেডার্স" : "Karim Traders",
            "01811-222333",
            new SaleInvoiceLine("BD-0042", header.Text(DocumentStrings.CattleBeef), "Sahiwal", header.Text(DocumentStrings.Male), "2 y 3 m", 320m, 250m, 80_000m),
            80_000m,
            30_000m,
            0m,
            50_000m,
            new DateOnly(2026, 12, 1),
            true,
            [new DocumentPaymentLine(new DateOnly(2026, 11, 1), "bKash", "TX-991", 30_000m)],
            AmountInWords.For(80_000m, language),
            "sale-invoice_INV-0042.pdf");

        var pdf = _renderer.RenderSaleInvoice(doc);

        pdf.Length.Should().BeGreaterThan(1000);
        IsPdf(pdf).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Payment_receipt_renders(ReportLanguage language, bool bengaliNumerals, bool withLogo)
    {
        var doc = new PaymentReceiptDocument(
            Header(language, bengaliNumerals, withLogo),
            "MR-20261105-1A2B3C4D",
            new DateOnly(2026, 11, 5),
            true,
            "Karim Traders",
            "01811-222333",
            30_000m,
            "Bank",
            "CHQ-77",
            "First instalment",
            "INV-0042",
            "Sale of BD-0042",
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 12, 1),
            80_000m,
            30_000m,
            0m,
            50_000m,
            AmountInWords.For(30_000m, language),
            "money-receipt_MR-20261105-1A2B3C4D.pdf");

        var pdf = _renderer.RenderPaymentReceipt(doc);

        pdf.Length.Should().BeGreaterThan(1000);
        IsPdf(pdf).Should().BeTrue();

        // The supplier-side voucher uses the same document.
        var voucher = _renderer.RenderPaymentReceipt(doc with { IsCollection = false, Notes = null, Reference = null });
        voucher.Length.Should().BeGreaterThan(1000);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Purchase_order_renders(ReportLanguage language, bool bengaliNumerals, bool withLogo)
    {
        var header = Header(language, bengaliNumerals, withLogo);
        var doc = new PurchaseOrderDocument(
            header,
            "PO-20261120-0007",
            new DateOnly(2026, 11, 20),
            new DateOnly(2026, 11, 27),
            header.Text(DocumentStrings.Approved),
            "Agro Feeds Ltd",
            "Mr. Hasan",
            "01911-000111",
            "sales@agrofeeds.com",
            "Tongi, Gazipur",
            [
                new PurchaseOrderDocumentLine(language == ReportLanguage.Bn ? "গমের ভুসি" : "Wheat bran", "WB-01", 10m, "Bag (50 kg)", 2_000m, 20_000m),
                new PurchaseOrderDocumentLine("FMD vaccine", null, 2m, "bottle", 350m, 700m),
            ],
            20_700m,
            "Deliver before Friday",
            AmountInWords.For(20_700m, language),
            "purchase-order_PO-20261120-0007.pdf");

        var pdf = _renderer.RenderPurchaseOrder(doc);

        pdf.Length.Should().BeGreaterThan(1000);
        IsPdf(pdf).Should().BeTrue();
    }

    [Fact]
    public void An_unreadable_logo_does_not_stop_a_document_rendering()
    {
        var header = DocumentTestKit.Header(logo: [0x89, 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 3]);
        var doc = new PurchaseOrderDocument(
            header, "PO-1", new DateOnly(2026, 11, 20), null, "Draft", "Supplier", null, null, null, null,
            [], 0m, null, AmountInWords.English(0m), "purchase-order_PO-1.pdf");

        var pdf = _renderer.RenderPurchaseOrder(doc);

        pdf.Length.Should().BeGreaterThan(1000);
    }

    [Fact]
    public async Task Report_letterhead_prints_the_logo_and_contact_line_in_pdf_and_excel()
    {
        var data = await ReportingTestKit.RunAsync(new StockTestReport(
            [new StockRow("A", "Bran", "kg", 10m, 40m, 400m, 1m)]));
        data = data with
        {
            Meta = data.Meta with
            {
                OrganizationName = "Rahman Agro",
                ContactLine = "Savar, Dhaka · 01711-000000 · info@rahmanagro.com",
                LogoUrl = "/uploads/organizations/logo.png",
                Logo = DocumentTestKit.TinyPng,
            },
        };

        var pdf = await new QuestPdfReportRenderer().RenderAsync(data);
        pdf.Length.Should().BeGreaterThan(1000);

        var xlsx = await new ClosedXmlReportRenderer().RenderAsync(data);
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        var cells = workbook.Worksheets.First().CellsUsed().Select(c => c.GetString()).ToList();
        cells.Should().Contain("Rahman Agro").And.Contain("Savar, Dhaka · 01711-000000 · info@rahmanagro.com");
    }

    [Fact]
    public void Logo_never_travels_inside_the_json_dataset()
    {
        var meta = ReportMetaHeader.Empty with { Logo = DocumentTestKit.TinyPng, LogoUrl = "/uploads/organizations/logo.png" };

        var json = System.Text.Json.JsonSerializer.Serialize(meta);

        json.Should().Contain("LogoUrl").And.NotContain("\"Logo\"");
    }
}
