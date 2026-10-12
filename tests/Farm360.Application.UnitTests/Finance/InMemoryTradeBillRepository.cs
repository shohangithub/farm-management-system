using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;

namespace Farm360.Application.UnitTests.Finance;

/// <summary>A list-backed bill register, so credit rules can be checked on real objects.</summary>
internal sealed class InMemoryTradeBillRepository : ITradeBillRepository
{
    public List<TradeBill> Bills { get; } = [];
    public List<TradeBillPayment> Payments { get; } = [];

    public Task<TradeBill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Bills.FirstOrDefault(b => b.Id == id));

    public Task<TradeBill?> FindBySourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult(Bills.FirstOrDefault(b => b.FarmId == farmId && b.SourceDocumentId == sourceDocumentId && b.Category == category && !b.IsCancelled));

    public Task<IReadOnlyList<TradeBill>> GetByFarmIdAsync(Guid farmId, TradeBillKind? kind = null, bool includeCancelled = false, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TradeBill>>(Bills
            .Where(b => b.FarmId == farmId && (kind is null || b.Kind == kind) && (includeCancelled || !b.IsCancelled))
            .ToList());

    public Task<TradeBillPayment?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Payments.FirstOrDefault(p => p.Id == paymentId));

    public Task<IReadOnlyList<TradeBillPayment>> GetPaymentsAsync(Guid billId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TradeBillPayment>>(Payments.Where(p => p.BillId == billId).ToList());

    public Task<IReadOnlyList<TradeBillPayment>> GetPaymentsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TradeBillPayment>>(Payments.Where(p => p.FarmId == farmId).ToList());

    public void Add(TradeBill bill) => Bills.Add(bill);

    public void Update(TradeBill bill)
    {
    }

    public void AddPayment(TradeBillPayment payment) => Payments.Add(payment);

    public void RemovePayment(TradeBillPayment payment) => Payments.Remove(payment);
}
