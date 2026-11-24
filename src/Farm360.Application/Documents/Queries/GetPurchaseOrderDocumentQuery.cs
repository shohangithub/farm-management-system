using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Reporting.Model;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Documents.Queries;

/// <summary>Gathers a purchase order for printing: supplier, ordered items with names and units, totals.</summary>
/// <param name="PurchaseOrderId">The purchase order.</param>
/// <param name="Language">"en" / "bn"; blank uses the farm's default report language.</param>
/// <param name="BengaliNumerals">Null uses the farm's "Bengali numerals" rule.</param>
public sealed record GetPurchaseOrderDocumentQuery(Guid PurchaseOrderId, string? Language = null, bool? BengaliNumerals = null)
    : IRequest<PurchaseOrderDocument>;

public sealed class GetPurchaseOrderDocumentQueryHandler : IRequestHandler<GetPurchaseOrderDocumentQuery, PurchaseOrderDocument>
{
    private readonly IPurchaseOrderRepository _orders;
    private readonly ISupplierRepository _suppliers;
    private readonly IInventoryItemRepository _items;
    private readonly IFarmAccessService _farmAccess;
    private readonly IDocumentHeaderBuilder _headers;

    public GetPurchaseOrderDocumentQueryHandler(
        IPurchaseOrderRepository orders,
        ISupplierRepository suppliers,
        IInventoryItemRepository items,
        IFarmAccessService farmAccess,
        IDocumentHeaderBuilder headers)
    {
        _orders = orders;
        _suppliers = suppliers;
        _items = items;
        _farmAccess = farmAccess;
        _headers = headers;
    }

    public async Task<PurchaseOrderDocument> Handle(GetPurchaseOrderDocumentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Tenant-filtered: another tenant's order is simply not found.
        var order = await _orders.GetByIdWithItemsAsync(request.PurchaseOrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), request.PurchaseOrderId);

        if (!await _farmAccess.CanAccessFarmAsync(order.FarmId, cancellationToken))
        {
            throw new ForbiddenAccessException("You do not have access to this farm.");
        }

        var header = await _headers.BuildAsync(order.FarmId, request.Language, request.BengaliNumerals, cancellationToken);
        var supplier = await _suppliers.GetByIdAsync(order.SupplierId, cancellationToken);

        // One look-up for the farm's items rather than one per line; stragglers fetched singly.
        var items = (await _items.GetByFarmIdAsync(order.FarmId, cancellationToken: cancellationToken))
            .ToDictionary(i => i.Id);

        var lines = new List<PurchaseOrderDocumentLine>(order.Items.Count);
        foreach (var line in order.Items)
        {
            if (!items.TryGetValue(line.InventoryItemId, out var item))
            {
                item = await _items.GetByIdAsync(line.InventoryItemId, cancellationToken);
                if (item is not null)
                {
                    items[item.Id] = item;
                }
            }

            lines.Add(ToLine(line, item));
        }

        var total = Math.Round(lines.Sum(l => l.TotalBdt), 2);

        return new PurchaseOrderDocument(
            header,
            order.PoNumber,
            order.OrderDate,
            order.ExpectedDeliveryDate,
            header.Text(StatusText(order.Status)),
            supplier?.Name ?? "—",
            Blank(supplier?.ContactPerson),
            Blank(supplier?.Phone),
            Blank(supplier?.Email),
            Blank(supplier?.Address),
            lines,
            total,
            Blank(order.Notes),
            AmountInWords.For(total, header.Language),
            $"purchase-order_{GetSaleInvoiceDocumentQueryHandler.FileSafe(order.PoNumber)}.pdf");
    }

    /// <summary>
    /// The line as the supplier ordered it: in the pack unit it was entered in ("4 × Bag (50 kg)")
    /// when there is one, otherwise in the item's base unit.
    /// </summary>
    internal static PurchaseOrderDocumentLine ToLine(PurchaseOrderItem line, InventoryItem? item)
    {
        var name = item?.Name ?? "—";
        var sku = string.IsNullOrWhiteSpace(item?.Sku) ? null : item.Sku;

        if (!string.IsNullOrWhiteSpace(line.EnteredUnitName) && line.EnteredQuantity is { } packs && packs > 0m)
        {
            return new PurchaseOrderDocumentLine(
                name, sku, packs, line.EnteredUnitName.Trim(), Math.Round(line.TotalCostBdt / packs, 2), line.TotalCostBdt);
        }

        return new PurchaseOrderDocumentLine(
            name, sku, line.Quantity, item?.UnitOfMeasure ?? string.Empty, line.UnitCostBdt, line.TotalCostBdt);
    }

    internal static LocalizedText StatusText(PurchaseOrderStatus status) => status switch
    {
        PurchaseOrderStatus.Draft => DocumentStrings.Draft,
        PurchaseOrderStatus.PendingApproval => DocumentStrings.PendingApproval,
        PurchaseOrderStatus.Approved => DocumentStrings.Approved,
        PurchaseOrderStatus.Fulfilled => DocumentStrings.Fulfilled,
        PurchaseOrderStatus.Cancelled => DocumentStrings.Cancelled,
        _ => status.ToString(),
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
