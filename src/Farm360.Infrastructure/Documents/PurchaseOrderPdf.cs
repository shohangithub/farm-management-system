using System.Collections.Generic;
using Farm360.Application.Documents;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Farm360.Infrastructure.Documents;

/// <summary>
/// The purchase order a farm sends its supplier: who it is to, what is ordered at what price,
/// when it is expected, the terms, and the approval signature.
/// </summary>
internal sealed class PurchaseOrderPdf : IDocument
{
    private readonly PurchaseOrderDocument _doc;

    public PurchaseOrderPdf(PurchaseOrderDocument document)
    {
        _doc = document;
    }

    private DocumentHeader H => _doc.Header;

    private string Title => H.Text(DocumentStrings.PurchaseOrder);

    public DocumentMetadata GetMetadata() => DocumentPdf.Metadata(H, $"{Title} {_doc.PoNumber}");

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
                    H.Text(DocumentStrings.Supplier),
                    _doc.SupplierName,
                    SupplierLines()));
                row.ConstantItem(8, Unit.Millimetre);
                row.RelativeItem(2).Element(c => DocumentPdf.Fields(c, Fields()));
            });

            col.Item().Element(ComposeItems);

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignBottom().Element(c => DocumentPdf.InWords(c, H, _doc.AmountInWords));
                row.ConstantItem(8, Unit.Millimetre);
                row.ConstantItem(70, Unit.Millimetre).Element(c => DocumentPdf.Totals(
                    c,
                    new List<DocumentTotal> { new(H.Text(DocumentStrings.GrandTotal), H.Money(_doc.GrandTotalBdt), Emphasis: true) }));
            });

            var notes = _doc.Notes ?? string.Empty;
            if (notes.Length > 0)
            {
                col.Item().Element(c => DocumentPdf.NoteBox(c, H.Text(DocumentStrings.Notes), notes));
            }

            col.Item().Element(c => DocumentPdf.NoteBox(c, H.Text(DocumentStrings.TermsTitle), H.Text(DocumentStrings.PoTerms)));

            col.Item().Element(c => DocumentPdf.Signatures(c, H, [DocumentStrings.PreparedBy, DocumentStrings.ApprovedBy]));
        });

    private List<string?> SupplierLines() =>
    [
        _doc.SupplierContactPerson is null ? null : $"{H.Text(DocumentStrings.ContactPerson)}: {_doc.SupplierContactPerson}",
        _doc.SupplierPhone is null ? null : $"{H.Text(DocumentStrings.Phone)}: {_doc.SupplierPhone}",
        _doc.SupplierEmail is null ? null : $"{H.Text(DocumentStrings.Email)}: {_doc.SupplierEmail}",
        _doc.SupplierAddress is null ? null : $"{H.Text(DocumentStrings.Address)}: {_doc.SupplierAddress}",
    ];

    private List<DocumentField> Fields()
    {
        var fields = new List<DocumentField>
        {
            new(H.Text(DocumentStrings.PoNo), _doc.PoNumber),
            new(H.Text(DocumentStrings.Date), H.Date(_doc.OrderDate)),
        };

        if (_doc.ExpectedDeliveryDate is { } expected)
        {
            fields.Add(new DocumentField(H.Text(DocumentStrings.ExpectedDelivery), H.Date(expected)));
        }

        fields.Add(new DocumentField(H.Text(DocumentStrings.Status), _doc.Status));
        return fields;
    }

    private void ComposeItems(IContainer container) =>
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(8, Unit.Millimetre);
                c.RelativeColumn();
                c.ConstantColumn(20, Unit.Millimetre);
                c.ConstantColumn(26, Unit.Millimetre);
                c.ConstantColumn(24, Unit.Millimetre);
                c.ConstantColumn(28, Unit.Millimetre);
            });

            table.Header(h =>
            {
                DocumentPdf.HeadText(h.Cell(), "#");
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Item));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Quantity), alignRight: true);
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.Unit));
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.UnitCost), alignRight: true);
                DocumentPdf.HeadText(h.Cell(), H.Text(DocumentStrings.LineTotal), alignRight: true);
            });

            var serial = 0;
            foreach (var line in _doc.Lines)
            {
                serial++;
                DocumentPdf.BodyText(table.Cell(), H.Number(serial, 0));
                DocumentPdf.BodyCell(table.Cell()).Column(cell =>
                {
                    cell.Item().Text(line.ItemName).SemiBold();
                    if (!string.IsNullOrWhiteSpace(line.Sku))
                    {
                        cell.Item().Text(line.Sku).FontSize(DocumentPdf.SmallFontSize).FontColor(DocumentPdf.Muted);
                    }
                });
                DocumentPdf.BodyText(table.Cell(), H.Number(line.Quantity, 2), alignRight: true);
                DocumentPdf.BodyText(table.Cell(), line.Unit);
                DocumentPdf.BodyText(table.Cell(), H.Money(line.UnitCostBdt), alignRight: true);
                DocumentPdf.BodyText(table.Cell(), H.Money(line.TotalBdt), alignRight: true);
            }

            if (_doc.Lines.Count == 0)
            {
                table.Cell().ColumnSpan(6).PaddingVertical(12).AlignCenter().Text("—").FontColor(DocumentPdf.Muted);
            }
        });
}
