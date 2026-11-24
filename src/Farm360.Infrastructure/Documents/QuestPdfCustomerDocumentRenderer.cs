using System;
using Farm360.Application.Documents;
using Farm360.Infrastructure.Reporting;
using QuestPDF.Fluent;

namespace Farm360.Infrastructure.Documents;

/// <summary>
/// Renders sale invoices, payment receipts and purchase orders with QuestPDF, on the same fonts
/// and letterhead as the report pack (Bengali shaped by HarfBuzz, text kept as vectors).
/// </summary>
public sealed class QuestPdfCustomerDocumentRenderer : ICustomerDocumentRenderer
{
    public byte[] RenderSaleInvoice(SaleInvoiceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReportFonts.EnsureRegistered();
        return new SaleInvoicePdf(document).GeneratePdf();
    }

    public byte[] RenderPaymentReceipt(PaymentReceiptDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReportFonts.EnsureRegistered();
        return new PaymentReceiptPdf(document).GeneratePdf();
    }

    public byte[] RenderPurchaseOrder(PurchaseOrderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ReportFonts.EnsureRegistered();
        return new PurchaseOrderPdf(document).GeneratePdf();
    }
}
