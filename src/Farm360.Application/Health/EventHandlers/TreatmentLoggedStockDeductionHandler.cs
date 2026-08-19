using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Health.EventHandlers;

public sealed class TreatmentLoggedStockDeductionHandler : INotificationHandler<TreatmentLoggedEvent>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _stockTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TreatmentLoggedStockDeductionHandler> _logger;

    public TreatmentLoggedStockDeductionHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository stockTransactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<TreatmentLoggedStockDeductionHandler> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _stockTransactionRepository = stockTransactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(TreatmentLoggedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.InventoryItemId == null || notification.ConsumptionQuantity == null || notification.ConsumptionQuantity <= 0)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing auto-stock deduction for TreatmentLoggedEvent (EventId: {EventId}, InventoryItemId: {InventoryItemId}, Consumption: {ConsumptionQuantity})",
                notification.EventId, notification.InventoryItemId, notification.ConsumptionQuantity);

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(notification.InventoryItemId.Value, cancellationToken);

        if (medicineItem == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Inventory item {InventoryItemId} not found. Cannot deduct stock for treatment {MedicalTreatmentId}.",
                    notification.InventoryItemId, notification.MedicalTreatmentId);
            return;
        }

        var costBdt = medicineItem.WeightedAverageCostBdt;

        medicineItem.DeductStock(notification.ConsumptionQuantity.Value, notification.MedicalTreatmentId);
        _inventoryItemRepository.Update(medicineItem);

        var transaction = new Domain.Inventory.StockTransaction(
            id: Guid.NewGuid(),
            tenantId: notification.TenantId,
            farmId: medicineItem.FarmId, // Assuming item belongs to the same farm
            inventoryItemId: medicineItem.Id,
            transactionType: StockTransactionType.AutoMedicineConsumption,
            quantity: notification.ConsumptionQuantity.Value,
            unitCostBdt: costBdt,
            balanceAfter: medicineItem.CurrentStock,
            transactionDate: DateOnly.FromDateTime(DateTime.UtcNow),
            reason: $"Auto-deduction for medical treatment on animal {notification.AnimalId}",
            referenceId: notification.MedicalTreatmentId
        );

        await _stockTransactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Successfully deducted {Quantity} units of {ItemName} for medical treatment {MedicalTreatmentId}",
                notification.ConsumptionQuantity, medicineItem.Name, notification.MedicalTreatmentId);
    }
}
