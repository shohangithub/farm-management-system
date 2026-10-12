using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Books stock returned to a supplier: Dr Cash (refund), Cr Inventory. It reduces the stock asset;
/// it is not revenue (it used to be counted as income, inflating profit). When the order was bought
/// on credit and still owed, the return reduces the supplier's bill instead (Dr Accounts Payable).
/// </summary>
public class PurchaseReturnCompletedFinanceHandler : INotificationHandler<PurchaseReturnCompletedNotification>
{
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PurchaseReturnCompletedFinanceHandler> _logger;
    private readonly ITradeCreditService? _credit;

    public PurchaseReturnCompletedFinanceHandler(
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork,
        ILogger<PurchaseReturnCompletedFinanceHandler> logger,
        ITradeCreditService? credit = null)
    {
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _credit = credit;
    }

    public async Task Handle(PurchaseReturnCompletedNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var evt = notification.DomainEvent;
        if (evt.TotalAmountBdt <= 0)
            return;

        var settlement = _credit is null
            ? SettlementAccount.Cash
            : await _credit.SettleReturnAsync(evt.FarmId, evt.PurchaseOrderId, evt.PurchaseReturnId, evt.TotalAmountBdt, cancellationToken);
        var onBill = settlement == SettlementAccount.Payable;

        await _ledgerPosting.PostAsync(new LedgerPosting(
            evt.TenantId,
            evt.FarmId,
            TransactionCategory.InventoryPurchaseReturn,
            evt.TotalAmountBdt,
            evt.ReturnDate.ToDateTime(TimeOnly.MinValue),
            evt.PurchaseReturnId,
            InventoryAccounting.Module,
            $"Purchase Return - {evt.ReturnNumber}",
            Settlement: settlement,
            Notes: onBill
                ? $"Deducted from the supplier's unpaid bill for stock returned under {evt.ReturnNumber}"
                : $"Supplier refund for stock returned under {evt.ReturnNumber}",
            ReferenceId: evt.ReturnNumber), cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted supplier refund of {Amount} BDT for Purchase Return {ReturnNumber}.", evt.TotalAmountBdt, evt.ReturnNumber);
        }
    }
}
