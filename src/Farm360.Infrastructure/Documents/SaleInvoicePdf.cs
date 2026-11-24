using System.Collections.Generic;
using Farm360.Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Documents;

/// <summary>
/// The invoice / receipt a farm hands the buyer of an animal: who bought it, what was sold
/// (tag, species, breed, sex, age, weight, rate), what was received and what is still due.
/// </summary>
internal sealed class SaleInvoicePdf : IDocument
{
    private readonly SaleInvoiceDocument _doc;

    public SaleInvoicePdf(SaleInvoiceDocument document)
    {
        _doc = document;
    }

    private DocumentHeader H => _doc.Header;

    private string Title => H.Text(DocumentStrings.SaleInvoice);

    public DocumentMetadata GetMetadata() => DocumentPdf.Metadata(H, $"{Title} {_doc.InvoiceNumber}");

    public void Compose(IDocumentContainer container) =>
        container.Page(page => DocumentPdf.Page(page, H, Title, ComposeBody));

    private void ComposeBody(IContainer container) =>
        container.Column(col =>
        {
            col.Spacing(10);

            col.Item().Row(row =>
            {
                row.RelativeItem(3).Element(c => DocumentPdf.PartyBox(
                    c,
                    H.Text(DocumentStrings.BilledTo),
                    _doc.BuyerName,
                    [_doc.BuyerPhone is null ? null : $"{H.Text(DocumentStrings.Phone)}: {_doc.BuyerPhone}"]));
                row.ConstantItem(8, Unit.Millimetre);
                row.RelativeItem(2).Element(c => DocumentPdf.Fields(c, Fields()));
            });

            col.Item().Element(ComposeLine);

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Element(c => DocumentPdf.InWords(c, H, _doc.AmountInWords));
                row.ConstantItem(8, Unit.Millimetre);
                row.ConstantItem(78, Unit.Millimetre).Element(c => DocumentPdf.Totals(c, Totals()));
            });

            if (_doc.Payments.Count > 0)
            {
                col.Item().Element(c => DocumentPdf.Payments(c, H, DocumentStrings.PaymentHistory, _doc.Payments));
            }

            col.Item().Element(c => DocumentPdf.Signatures(c, H, [DocumentStrings.ReceivedBy, DocumentStrings.AuthorisedSignature]));
            col.Item().Element(c => DocumentPdf.Closing(c, H.Text(DocumentStrings.ThankYou)));
        });

    private List<DocumentField> Fields()
    {
        var fields = new List<DocumentField>
        {
            new(H.Text(DocumentStrings.InvoiceNo), _doc.InvoiceNumber),
            new(H.Text(DocumentStrings.Date), H.Date(_doc.InvoiceDate)),
            new(H.Text(DocumentStrings.Status), H.Text(_doc.IsCreditSale ? DocumentStrings.CreditSale : DocumentStrings.CashSale)),
        };

        if (_doc.IsCreditSale && _doc.BalanceDueBdt > 0 && _doc.DueDate is { } due)
        {
            fields.Add(new DocumentField(H.Text(DocumentStrings.DueDate), H.Date(due)));
        }

        return fields;
    }

    private void ComposeLine(IContainer container)
    {
        var line = _doc.Line;

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(7, Unit.Millimetre);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.4f);
                c.RelativeColumn(1.4f);
                c.RelativeColumn(1f);
                c.RelativeColumn(1.1f);
                c.ConstantColumn(19, Unit.Millimetre);
                c.ConstantColumn(20, Unit.Millimetre);
                c.ConstantColumn(27, Unit.Millimetre);
            });

            table.Header(h =>
            {
                DocumentPdf.HeadText(h.Cell(), "#");
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Tag));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Species));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Breed));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Sex));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Age));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.WeightKg), alignRight: true);
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.PricePerKg), alignRight: true);
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.AmountBdt), alignRight: true);
            });

            DocumentPdf.BodyText(table.Cell(), H.Digits("1"));
            DocumentPdf.BodyCell(table.Cell()).Text(line.Tag).SemiBold();
            DocumentPdf.BodyText(table.Cell(), line.Species);
            DocumentPdf.BodyText(table.Cell(), line.Breed ?? "—");
            DocumentPdf.BodyText(table.Cell(), line.Sex);
            DocumentPdf.BodyText(table.Cell(), line.Age);
            DocumentPdf.BodyText(table.Cell(), line.WeightKg is { } kg ? H.Number(kg, 1) : "—", alignRight: true);
            DocumentPdf.BodyText(table.Cell(), line.PricePerKgBdt is { } rate ? H.Money(rate) : "—", alignRight: true);
            DocumentPdf.BodyCell(table.Cell()).AlignRight().Text(H.Money(line.AmountBdt)).SemiBold();
        });
    }

    private List<DocumentTotal> Totals()
    {
        var totals = new List<DocumentTotal>
        {
            new(H.Text(DocumentStrings.Total), H.Money(_doc.AmountBdt)),
            new(H.Text(DocumentStrings.Received), H.Money(_doc.ReceivedBdt)),
        };

        if (_doc.AdjustedBdt > 0)
        {
            totals.Add(new DocumentTotal(H.Text(DocumentStrings.Adjusted), H.Money(_doc.AdjustedBdt)));
        }

        totals.Add(new DocumentTotal(H.Text(DocumentStrings.BalanceDue), H.Money(_doc.BalanceDueBdt), Emphasis: true));
        return totals;
    }
}
