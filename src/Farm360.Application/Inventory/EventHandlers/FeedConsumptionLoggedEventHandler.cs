using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Feeding.Events;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record FeedConsumptionLoggedNotification(FeedConsumptionLoggedEvent DomainEvent) : INotification;

/// <summary>
/// Takes a logged feed consumption out of stock: the logged recipe's net kg is split into its
/// ingredients (normalised %), each converted to its stock item's base unit and deducted from that
/// item — exactly as a confirmed daily feeding entry is. (It used to deduct the whole net kg from
/// whichever Feed item happened to have stock.)
/// </summary>
public sealed class FeedConsumptionLoggedEventHandler : INotificationHandler<FeedConsumptionLoggedNotification>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _ingredientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<FeedConsumptionLoggedEventHandler> _logger;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly Units.IUnitService? _units;

    public FeedConsumptionLoggedEventHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<FeedConsumptionLoggedEventHandler> logger,
        ILedgerPostingService ledgerPosting,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository ingredientRepository,
        Units.IUnitService? units = null)
    {
        _ledgerPosting = ledgerPosting;
        _inventoryItemRepository = inventoryItemRepository;
        _transactionRepository = transactionRepository;
        _formulaRepository = formulaRepository;
        _ingredientRepository = ingredientRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _units = units;
    }

    public async Task Handle(FeedConsumptionLoggedNotification wrapper, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        var notification = wrapper.DomainEvent;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Processing auto-stock deduction for FeedConsumptionLoggedEvent (LogId: {LogId}, NetKg: {NetKg})",
                notification.LogId, notification.NetConsumptionKg);
        }

        if (notification.NetConsumptionKg <= 0)
            return;

        var formula = await _formulaRepository.GetByIdAsync(notification.FormulaId, cancellationToken);
        if (formula is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Feed log {LogId}: recipe {FormulaId} not found, nothing deducted from stock.", notification.LogId, notification.FormulaId);
            return;
        }

        var calculator = new FeedCostCalculator(_ingredientRepository, _inventoryItemRepository, _units);
        var requirement = await calculator.RequirementAsync(formula, notification.NetConsumptionKg, cancellationToken);

        var deducted = false;
        foreach (var line in requirement.Ingredients)
        {
            if (line.Item is not { } item || line.StockQuantity <= 0)
                continue;

            if (line.IsShort)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Feed log {LogId}: '{ItemName}' short of stock (needed {Needed} {Unit}, have {Available}); not deducted.",
                        notification.LogId, item.Name, line.StockQuantity, item.UnitOfMeasure, item.CurrentStock);
                }
                continue;
            }

            var txId = Guid.NewGuid();
            item.DeductStock(line.StockQuantity, txId);

            var tx = new StockTransaction(
                txId,
                notification.TenantId,
                notification.FarmId,
                item.Id,
                StockTransactionType.AutoFeedConsumption,
                line.StockQuantity,
                item.WeightedAverageCostBdt,
                item.CurrentStock,
                notification.LogDate,
                reason: $"Automated feed deduction from Feed Log #{notification.LogId}",
                referenceId: notification.LogId);

            _inventoryItemRepository.Update(item);
            await _transactionRepository.AddAsync(tx, cancellationToken);

            // Feed issued from stock becomes feed cost (Cr Inventory) — keyed on the log and the item.
            await InventoryAccounting.PostConsumptionAsync(
                _ledgerPosting,
                item,
                line.StockQuantity,
                item.WeightedAverageCostBdt,
                notification.LogDate,
                LedgerPosting.SubKey(notification.LogId, item.Id.ToString("N")),
                $"Feed consumption log - {item.Name}",
                cancellationToken: cancellationToken);

            deducted = true;
        }

        if (!deducted)
            return;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Deducted feed log {LogId} ({NetKg} kg of '{Formula}') from its ingredients' stock.",
                notification.LogId, notification.NetConsumptionKg, formula.Title);
        }
    }
}
