using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;

namespace Farm360.Application.Documents.Queries;

/// <summary>
/// Gathers the receipt for one payment recorded in Payables &amp; Receivables: a money receipt for
/// a collection from a customer, a payment voucher for money paid to a supplier.
/// </summary>
/// <param name="PaymentId">The <see cref="TradeBillPayment"/> id.</param>
/// <param name="Language">"en" / "bn"; blank uses the farm's default report language.</param>
/// <param name="BengaliNumerals">Null uses the farm's "Bengali numerals" rule.</param>
public sealed record GetPaymentReceiptDocumentQuery(Guid PaymentId, string? Language = null, bool? BengaliNumerals = null)
    : IRequest<PaymentReceiptDocument>;

public sealed class GetPaymentReceiptDocumentQueryHandler : IRequestHandler<GetPaymentReceiptDocumentQuery, PaymentReceiptDocument>
{
    private readonly ITradeBillRepository _bills;
    private readonly IFarmAccessService _farmAccess;
    private readonly IDocumentHeaderBuilder _headers;

    public GetPaymentReceiptDocumentQueryHandler(
        ITradeBillRepository bills,
        IFarmAccessService farmAccess,
        IDocumentHeaderBuilder headers)
    {
        _bills = bills;
        _farmAccess = farmAccess;
        _headers = headers;
    }

    public async Task<PaymentReceiptDocument> Handle(GetPaymentReceiptDocumentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Tenant-filtered: a payment of another tenant is simply not found.
        var payment = await _bills.GetPaymentByIdAsync(request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(TradeBillPayment), request.PaymentId);

        if (!await _farmAccess.CanAccessFarmAsync(payment.FarmId, cancellationToken))
        {
            throw new ForbiddenAccessException("You do not have access to this farm.");
        }

        var bill = await _bills.GetByIdAsync(payment.BillId, cancellationToken);
        if (bill is null || bill.FarmId != payment.FarmId)
        {
            throw new NotFoundException(nameof(TradeBill), payment.BillId);
        }

        var header = await _headers.BuildAsync(payment.FarmId, request.Language, request.BengaliNumerals, cancellationToken);

        // What had been paid once this payment was in — the balance the receipt hands over.
        var all = await _bills.GetPaymentsAsync(bill.Id, cancellationToken);
        var paidThrough = all
            .Where(p => p.Id == payment.Id || IsEarlier(p, payment))
            .Sum(p => p.AmountBdt);
        if (!all.Any(p => p.Id == payment.Id))
        {
            paidThrough += payment.AmountBdt;
        }

        var balance = Math.Round(bill.AmountBdt - paidThrough - bill.AdjustedBdt, 2);
        var isCollection = bill.Kind == TradeBillKind.Receivable;
        var receiptNumber = ReceiptNumber(payment, isCollection);

        return new PaymentReceiptDocument(
            header,
            receiptNumber,
            payment.PaymentDate,
            isCollection,
            bill.PartyName,
            string.IsNullOrWhiteSpace(bill.PartyPhone) ? null : bill.PartyPhone,
            payment.AmountBdt,
            payment.Method,
            string.IsNullOrWhiteSpace(payment.Reference) ? null : payment.Reference,
            string.IsNullOrWhiteSpace(payment.Notes) ? null : payment.Notes,
            bill.ReferenceNo,
            bill.Description,
            bill.BillDate,
            bill.DueDate,
            bill.AmountBdt,
            Math.Round(paidThrough, 2),
            bill.AdjustedBdt,
            Math.Max(0m, balance),
            AmountInWords.For(payment.AmountBdt, header.Language),
            $"{(isCollection ? "money-receipt" : "payment-voucher")}_{GetSaleInvoiceDocumentQueryHandler.FileSafe(receiptNumber)}.pdf");
    }

    /// <summary>"MR-20261124-1A2B3C4D" for a collection, "PV-…" for a supplier payment — stable per payment.</summary>
    internal static string ReceiptNumber(TradeBillPayment payment, bool isCollection)
    {
        var prefix = isCollection ? "MR" : "PV";
        var date = payment.PaymentDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var id = payment.Id.ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();
        return $"{prefix}-{date}-{id}";
    }

    private static bool IsEarlier(TradeBillPayment candidate, TradeBillPayment payment) =>
        candidate.PaymentDate < payment.PaymentDate
        || (candidate.PaymentDate == payment.PaymentDate && candidate.CreatedAtUtc < payment.CreatedAtUtc);
}
