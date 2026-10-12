using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Contracts.Finance;
using Farm360.Domain.Finance;

namespace Farm360.Application.Finance.Services;

public static class TradeBillMapping
{
    /// <summary>Aging buckets by days past the due date, oldest last.</summary>
    public static readonly (string Key, string Label, int FromDays, int ToDays)[] AgingBuckets =
    [
        ("current", "Not yet due", int.MinValue, 0),
        ("1-30", "1–30 days overdue", 1, 30),
        ("31-60", "31–60 days overdue", 31, 60),
        ("61-90", "61–90 days overdue", 61, 90),
        ("90+", "Over 90 days overdue", 91, int.MaxValue),
    ];

    public static int DaysOverdue(TradeBill bill, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(bill);
        if (!bill.IsOpen || bill.DueDate is not { } due)
            return 0;
        return Math.Max(0, asOf.DayNumber - due.DayNumber);
    }

    public static string Bucket(int daysOverdue) =>
        AgingBuckets.First(b => daysOverdue >= b.FromDays && daysOverdue <= b.ToDays).Key;

    public static TradeBillPaymentDto ToDto(this TradeBillPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new TradeBillPaymentDto(payment.Id, payment.BillId, payment.PaymentDate, payment.AmountBdt, payment.Method, payment.Reference, payment.Notes);
    }

    public static TradeBillDto ToDto(this TradeBill bill, IEnumerable<TradeBillPayment>? payments, DateOnly asOf)
    {
        ArgumentNullException.ThrowIfNull(bill);
        var overdue = DaysOverdue(bill, asOf);
        return new TradeBillDto(
            bill.Id,
            bill.FarmId,
            bill.Kind.ToString(),
            bill.Status.ToString(),
            bill.PartyName,
            bill.PartyPhone,
            bill.SupplierId,
            bill.SourceDocumentId,
            bill.Category.ToString(),
            AccountChart.For(bill.Category).Name,
            bill.SourceModule,
            bill.ReferenceNo,
            bill.Description,
            bill.Notes,
            bill.BillDate,
            bill.DueDate,
            overdue,
            Bucket(overdue),
            bill.AmountBdt,
            bill.PaidBdt,
            bill.AdjustedBdt,
            Math.Round(bill.OutstandingBdt, 2),
            (payments ?? []).OrderBy(p => p.PaymentDate).Select(p => p.ToDto()).ToList());
    }
}
