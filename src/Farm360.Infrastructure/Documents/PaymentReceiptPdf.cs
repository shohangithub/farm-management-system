using System.Collections.Generic;
using Farm360.Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Documents;

/// <summary>
/// A money receipt (a customer paid the farm) or a payment voucher (the farm paid a supplier) for
/// one payment recorded against a credit bill, showing what is left on the bill afterwards.
/// </summary>
internal sealed class PaymentReceiptPdf : IDocument
{
    private readonly PaymentReceiptDocument _doc;

    public PaymentReceiptPdf(PaymentReceiptDocument document)
    {
        _doc = document;
    }

    private DocumentHeader H => _doc.Header;

    private string Title => H.Text(_doc.IsCollection ? DocumentStrings.MoneyReceipt : DocumentStrings.PaymentVoucher);

    public DocumentMetadata GetMetadata() => DocumentPdf.Metadata(H, $"{Title} {_doc.ReceiptNumber}");

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
                    H.Text(_doc.IsCollection ? DocumentStrings.ReceivedFrom : DocumentStrings.PaidTo),
                    _doc.PartyName,
                    [_doc.PartyPhone is null ? null : $"{H.Text(DocumentStrings.Phone)}: {_doc.PartyPhone}"]));
                row.ConstantItem(8, Unit.Millimetre);
                row.RelativeItem(2).Element(c => DocumentPdf.Fields(c, Fields()));
            });

            // The amount, large — the one figure a receipt exists to prove.
            col.Item().Border(1).BorderColor(Colors.Black).Background(DocumentPdf.HeaderFill).Padding(8).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(H.Text(_doc.IsCollection ? DocumentStrings.AmountReceived : DocumentStrings.AmountPaid))
                        .FontSize(DocumentPdf.SmallFontSize + 0.5f).FontColor(DocumentPdf.Muted);
                    left.Item().PaddingTop(2).Text(_doc.AmountInWords).SemiBold();
                });
                row.AutoItem().AlignMiddle().Text($"BDT {H.Money(_doc.AmountBdt)}").FontSize(16).Bold();
            });

            col.Item().Element(ComposeBill);

            var notes = _doc.Notes ?? string.Empty;
            if (notes.Length > 0)
            {
                col.Item().Element(c => DocumentPdf.NoteBox(c, H.Text(DocumentStrings.Notes), notes));
            }

            col.Item().Element(c => DocumentPdf.Signatures(c, H, [DocumentStrings.ReceivedBy, DocumentStrings.AuthorisedSignature]));

            if (_doc.IsCollection)
            {
                col.Item().Element(c => DocumentPdf.Closing(c, H.Text(DocumentStrings.ThankYou)));
            }
        });

    private List<DocumentField> Fields()
    {
        var fields = new List<DocumentField>
        {
            new(H.Text(_doc.IsCollection ? DocumentStrings.ReceiptNo : DocumentStrings.VoucherNo), _doc.ReceiptNumber),
            new(H.Text(DocumentStrings.Date), H.Date(_doc.PaymentDate)),
            new(H.Text(DocumentStrings.Method), _doc.Method),
        };

        var reference = _doc.Reference ?? string.Empty;
        if (reference.Length > 0)
        {
            fields.Add(new DocumentField(H.Text(DocumentStrings.Reference), reference));
        }

        return fields;
    }

    /// <summary>The bill this payment settles and where it stands once the payment is in.</summary>
    private void ComposeBill(IContainer container) =>
        container.Column(col =>
        {
            col.Item().PaddingBottom(3).Text(H.Text(DocumentStrings.AgainstBill)).Bold();
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.ConstantColumn(34, Unit.Millimetre);
                    c.ConstantColumn(26, Unit.Millimetre);
                    c.RelativeColumn();
                    c.ConstantColumn(30, Unit.Millimetre);
                });

                table.Header(h =>
                {
                    DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Reference));
                    DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.BillDate));
                    DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Description));
                    DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.BillAmount), alignRight: true);
                });

                DocumentPdf.BodyCell(table.Cell()).Text(_doc.BillReference).SemiBold();
                DocumentPdf.BodyText(table.Cell(), H.Date(_doc.BillDate));
                DocumentPdf.BodyText(table.Cell(), _doc.BillDescription);
                DocumentPdf.BodyText(table.Cell(), H.Money(_doc.BillAmountBdt), alignRight: true);
            });

            col.Item().PaddingTop(8).AlignRight().Width(82, Unit.Millimetre).Element(c => DocumentPdf.Totals(c, Totals()));
        });

    private List<DocumentTotal> Totals()
    {
        var totals = new List<DocumentTotal>
        {
            new(H.Text(DocumentStrings.BillAmount), H.Money(_doc.BillAmountBdt)),
            new(H.Text(DocumentStrings.PaidToDate), H.Money(_doc.PaidToDateBdt)),
        };

        if (_doc.AdjustedBdt > 0)
        {
            totals.Add(new DocumentTotal(H.Text(DocumentStrings.Adjusted), H.Money(_doc.AdjustedBdt)));
        }

        totals.Add(new DocumentTotal(H.Text(DocumentStrings.BalanceRemaining), H.Money(_doc.BalanceRemainingBdt), Emphasis: true));

        if (_doc.BalanceRemainingBdt > 0 && _doc.BillDueDate is { } due)
        {
            totals.Add(new DocumentTotal(H.Text(DocumentStrings.DueDate), H.Date(due)));
        }

        return totals;
    }
}
