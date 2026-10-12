using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Inventory.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Books consumables used from stock: Dr Consumables expense, Cr Inventory. Consumable entries
/// always deduct stock, so no cash moves here — it moved when the stock was bought.
/// </summary>
public sealed class DailyConsumableCostCalculatedFinanceEventHandler : INotificationHandler<DailyConsumableCostCalculatedEvent>
{
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DailyConsumableCostCalculatedFinanceEventHandler> _logger;

    public DailyConsumableCostCalculatedFinanceEventHandler(
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork,
        ILogger<DailyConsumableCostCalculatedFinanceEventHandler> logger)
    {
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyConsumableCostCalculatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TotalCostBdt <= 0)
            return;

        try
        {
            await _ledgerPosting.PostAsync(new LedgerPosting(
                notification.TenantId,
                notification.FarmId,
                TransactionCategory.ConsumableExpense,
                notification.TotalCostBdt,
                notification.EntryDate.ToDateTime(TimeOnly.MinValue),
                notification.EntryId,
                InventoryAccounting.Module,
                $"Daily Consumable Usage - {notification.ItemName}",
                Settlement: SettlementAccount.Inventory,
                Notes: $"Daily consumable usage: {notification.ActualQuantity:0.##} ({notification.ItemName}) from stock"), cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post automated financial transaction for daily consumable entry {EntryId}", notification.EntryId);
        }
#pragma warning restore CA1031
    }
}
