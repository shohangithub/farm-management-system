using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record FeedConsumptionLoggedNotification(FeedConsumptionLoggedEvent DomainEvent) : INotification;

public sealed class FeedConsumptionLoggedEventHandler : INotificationHandler<FeedConsumptionLoggedNotification>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FeedConsumptionLoggedEventHandler> _logger;

    public FeedConsumptionLoggedEventHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<FeedConsumptionLoggedEventHandler> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(FeedConsumptionLoggedNotification wrapper, CancellationToken cancellationToken)
    {
        var notification = wrapper.DomainEvent;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Processing auto-stock deduction for FeedConsumptionLoggedEvent (LogId: {LogId}, NetKg: {NetKg})",
                notification.LogId, notification.NetConsumptionKg);
        }

        var items = await _inventoryItemRepository.GetByFarmIdAsync(notification.FarmId, InventoryCategory.Feed, cancellationToken);
        if (items.Count == 0) return;

        var feedItem = items.FirstOrDefault(x => x.IsActive && x.CurrentStock > 0);
        if (feedItem != null && feedItem.CurrentStock >= notification.NetConsumptionKg)
        {
            var txId = Guid.NewGuid();
            feedItem.DeductStock(notification.NetConsumptionKg, txId);

            var tx = new StockTransaction(
                txId,
                notification.TenantId,
                notification.FarmId,
                feedItem.Id,
                StockTransactionType.AutoFeedConsumption,
                notification.NetConsumptionKg,
                feedItem.WeightedAverageCostBdt,
                feedItem.CurrentStock,
                notification.LogDate,
                reason: $"Automated feed deduction from Feed Log #{notification.LogId}",
                referenceId: notification.LogId);

            _inventoryItemRepository.Update(feedItem);
            await _transactionRepository.AddAsync(tx, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully deducted {NetKg} kg from feed item '{ItemName}' (Remaining: {Remaining} kg)",
                    notification.NetConsumptionKg, feedItem.Name, feedItem.CurrentStock);
            }
        }
    }
}
