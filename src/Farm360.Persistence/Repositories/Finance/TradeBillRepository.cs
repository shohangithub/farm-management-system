using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Persistence.Repositories.Finance;

public class TradeBillRepository : ITradeBillRepository
{
    private readonly ApplicationDbContext _context;

    public TradeBillRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TradeBill?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TradeBills.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<TradeBill?> FindBySourceAsync(Guid farmId, Guid sourceDocumentId, TransactionCategory category, CancellationToken cancellationToken = default)
    {
        // A bill added earlier in this unit of work is not in the database yet.
        var pending = _context.TradeBills.Local.FirstOrDefault(b =>
            b.FarmId == farmId && b.SourceDocumentId == sourceDocumentId && b.Category == category && !b.IsCancelled);
        if (pending is not null)
            return pending;

        return await _context.TradeBills
            .Where(b => b.FarmId == farmId && b.SourceDocumentId == sourceDocumentId && b.Category == category && !b.IsCancelled)
            .OrderByDescending(b => b.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradeBill>> GetByFarmIdAsync(Guid farmId, TradeBillKind? kind = null, bool includeCancelled = false, CancellationToken cancellationToken = default)
    {
        var query = _context.TradeBills.Where(b => b.FarmId == farmId);
        if (kind is { } k)
            query = query.Where(b => b.Kind == k);
        if (!includeCancelled)
            query = query.Where(b => !b.IsCancelled);

        return await query
            .OrderByDescending(b => b.BillDate)
            .ThenByDescending(b => b.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TradeBillPayment?> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.TradeBillPayments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
    }

    public async Task<IReadOnlyList<TradeBillPayment>> GetPaymentsAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        return await _context.TradeBillPayments
            .Where(p => p.BillId == billId)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TradeBillPayment>> GetPaymentsByFarmIdAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await _context.TradeBillPayments
            .Where(p => p.FarmId == farmId)
            .OrderBy(p => p.PaymentDate)
            .ToListAsync(cancellationToken);
    }

    public void Add(TradeBill bill) => _context.TradeBills.Add(bill);

    public void Update(TradeBill bill)
    {
        // Tracked bills are saved by change detection; only attach ones loaded elsewhere.
        if (_context.Entry(bill).State == EntityState.Detached)
            _context.TradeBills.Update(bill);
    }

    public void AddPayment(TradeBillPayment payment) => _context.TradeBillPayments.Add(payment);

    public void RemovePayment(TradeBillPayment payment) => _context.TradeBillPayments.Remove(payment);
}
