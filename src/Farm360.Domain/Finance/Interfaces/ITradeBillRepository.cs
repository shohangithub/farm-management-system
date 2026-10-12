using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Domain.Finance.Enums;

namespace Farm360.Domain.Finance.Interfaces;

public interface ITradeBillRepository
{
    Task<TradeBill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The live (not cancelled) bill raised for a source posting, if any.</summary>
    Task<TradeBill?> FindBySourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeBill>> GetByFarmIdAsync(Guid farmId, TradeBillKind? kind = null, bool includeCancelled = false, CancellationToken cancellationToken = default);

    Task<TradeBillPayment?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeBillPayment>> GetPaymentsAsync(Guid billId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeBillPayment>> GetPaymentsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default);

    void Add(TradeBill bill);
    void Update(TradeBill bill);
    void AddPayment(TradeBillPayment payment);
    void RemovePayment(TradeBillPayment payment);
}
