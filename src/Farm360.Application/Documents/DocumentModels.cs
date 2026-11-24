using System;
using System.Collections.Generic;
using Farm360.Application.Reporting.Model;

namespace Farm360.Application.Documents;

/// <summary>
/// The top of every customer document — who issued it, in which language — plus the formatting
/// helpers that keep figures and dates identical to the report pack (same formatter, same
/// numeral choice).
/// </summary>
public sealed record DocumentHeader(
    string OrganizationName,
    string? FarmName,
    string? ContactLine,
    byte[]? Logo,
    ReportLanguage Language,
    bool BengaliNumerals,
    string GeneratedByName,
    string GeneratedAtDisplay,
    DateOnly Today)
{
    public string Text(LocalizedText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.For(Language);
    }

    public string Money(decimal value) =>
        ReportValueFormatter.Format(value, ReportColumnType.Money, 2, BengaliNumerals, Language);

    public string Number(decimal value, int decimals) =>
        ReportValueFormatter.Format(value, ReportColumnType.Number, decimals, BengaliNumerals, Language);

    public string Date(DateOnly value) =>
        ReportValueFormatter.Format(value, ReportColumnType.Date, 0, BengaliNumerals, Language);

    /// <summary>Digits in the document's numeral system (identifiers excluded — call only for figures).</summary>
    public string Digits(string text) => BengaliNumerals ? ReportValueFormatter.ToBengaliNumerals(text) : text;
}

/// <summary>A payment already received against a document.</summary>
public sealed record DocumentPaymentLine(DateOnly Date, string Method, string? Reference, decimal AmountBdt);

/// <summary>The animal sold, as it is printed on the invoice line.</summary>
public sealed record SaleInvoiceLine(
    string Tag,
    string Species,
    string? Breed,
    string Sex,
    string Age,
    decimal? WeightKg,
    decimal? PricePerKgBdt,
    decimal AmountBdt);

/// <summary>Everything a sale invoice / receipt prints for one sold animal.</summary>
public sealed record SaleInvoiceDocument(
    DocumentHeader Header,
    string InvoiceNumber,
    DateOnly InvoiceDate,
    string BuyerName,
    string? BuyerPhone,
    SaleInvoiceLine Line,
    decimal AmountBdt,
    decimal ReceivedBdt,
    decimal AdjustedBdt,
    decimal BalanceDueBdt,
    DateOnly? DueDate,
    bool IsCreditSale,
    IReadOnlyList<DocumentPaymentLine> Payments,
    string AmountInWords,
    string FileName);

/// <summary>A receipt for money collected from a customer, or a voucher for money paid to a supplier.</summary>
public sealed record PaymentReceiptDocument(
    DocumentHeader Header,
    string ReceiptNumber,
    DateOnly PaymentDate,
    bool IsCollection,
    string PartyName,
    string? PartyPhone,
    decimal AmountBdt,
    string Method,
    string? Reference,
    string? Notes,
    string BillReference,
    string BillDescription,
    DateOnly BillDate,
    DateOnly? BillDueDate,
    decimal BillAmountBdt,
    decimal PaidToDateBdt,
    decimal AdjustedBdt,
    decimal BalanceRemainingBdt,
    string AmountInWords,
    string FileName);

/// <summary>One ordered item on a purchase order.</summary>
public sealed record PurchaseOrderDocumentLine(
    string ItemName,
    string? Sku,
    decimal Quantity,
    string Unit,
    decimal UnitCostBdt,
    decimal TotalBdt);

/// <summary>Everything a purchase order prints.</summary>
public sealed record PurchaseOrderDocument(
    DocumentHeader Header,
    string PoNumber,
    DateOnly OrderDate,
    DateOnly? ExpectedDeliveryDate,
    string Status,
    string SupplierName,
    string? SupplierContactPerson,
    string? SupplierPhone,
    string? SupplierEmail,
    string? SupplierAddress,
    IReadOnlyList<PurchaseOrderDocumentLine> Lines,
    decimal GrandTotalBdt,
    string? Notes,
    string AmountInWords,
    string FileName);

/// <summary>
/// Renders customer documents. Implemented with QuestPDF in Infrastructure, sharing the report
/// pack's letterhead and fonts so an invoice looks like it came from the same farm as its reports.
/// </summary>
public interface ICustomerDocumentRenderer
{
    byte[] RenderSaleInvoice(SaleInvoiceDocument document);

    byte[] RenderPaymentReceipt(PaymentReceiptDocument document);

    byte[] RenderPurchaseOrder(PurchaseOrderDocument document);
}
