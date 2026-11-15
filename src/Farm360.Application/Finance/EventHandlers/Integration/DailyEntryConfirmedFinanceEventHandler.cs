using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Books a confirmed feeding entry's cost.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Feed taken from stock: Dr Feed expense, Cr Inventory — the cash already left when the stock
/// was bought, so posting it as cash again was the old double count.</item>
/// <item>Ingredients not tracked in stock (fresh grass, fodder bought daily): Dr Feed expense, Cr Cash.</item>
/// </list>
/// Both postings are keyed on the entry, so a re-confirmed or adjusted entry restates them.
/// </remarks>
public sealed class DailyEntryConfirmedFinanceEventHandler : INotificationHandler<DailyFeedingCostCalculatedEvent>
{
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Farm360.Domain.Feeding.Interfaces.Repositories.IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly ILogger<DailyEntryConfirmedFinanceEventHandler> _logger;

    public DailyEntryConfirmedFinanceEventHandler(
        ILedgerPostingService ledgerPosting,
        IAnimalCostLedgerRepository ledgerRepository,
        IUnitOfWork unitOfWork,
        ILogger<DailyEntryConfirmedFinanceEventHandler> logger,
        Farm360.Domain.Feeding.Interfaces.Repositories.IAnimalFeedAllocationRepository? allocationRepository = null)
    {
        _allocationRepository = allocationRepository;
        _ledgerPosting = ledgerPosting;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(DailyFeedingCostCalculatedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TotalCostBdt <= 0)
            return;

        var date = notification.EntryDate.ToDateTime(TimeOnly.MinValue);
        var description = $"Feed Consumption on {notification.EntryDate:dd-MMM-yyyy}";

        await _ledgerPosting.PostAsync(new LedgerPosting(
            notification.TenantId, notification.FarmId, TransactionCategory.FeedCost,
            notification.StockCostBdt, date, notification.EntryId, "Feeding", description,
            Settlement: SettlementAccount.Inventory,
            Notes: $"Daily feeding: {notification.ActualKg:0.##} kg — feed issued from stock",
            AnimalId: notification.AnimalId, BatchId: notification.BatchId, ShedId: notification.ShedId), cancellationToken);

        await _ledgerPosting.PostAsync(new LedgerPosting(
            notification.TenantId, notification.FarmId, TransactionCategory.FeedCost,
            notification.DirectCostBdt, date, LedgerPosting.SubKey(notification.EntryId, "direct-feed"), "Feeding",
            $"{description} (not stock-tracked)",
            Notes: "Ingredients not linked to inventory — paid directly",
            AnimalId: notification.AnimalId, BatchId: notification.BatchId, ShedId: notification.ShedId), cancellationToken);

        // The animal cost ledger is NOT accumulated here: adding the entry's cost on every confirmation
        // double counted adjustments and re-confirmations, and missed group-fed animals entirely.
        // DailyEntryConfirmedEventHandler sets each touched animal's feed bucket from its confirmed
        // allocation total (idempotent). Re-setting it here is harmless and covers single-animal plans
        // when this handler runs on its own.
        if (notification.AnimalId.HasValue && _allocationRepository is not null)
        {
            await Farm360.Application.Feeding.Services.AnimalFeedCostSync.SyncAsync(
                _allocationRepository, _ledgerRepository, notification.TenantId, notification.FarmId,
                [notification.AnimalId.Value], cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted feed cost {Total} BDT ({Stock} from stock) for DailyFeedingEntry {EntryId}",
                notification.TotalCostBdt, notification.StockCostBdt, notification.EntryId);
        }
    }
}
