using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record VaccinationAdministeredNotification(VaccinationAdministeredEvent DomainEvent) : INotification;

public sealed class VaccinationAdministeredStockDeductionHandler : INotificationHandler<VaccinationAdministeredNotification>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaccinationAdministeredStockDeductionHandler> _logger;

    public VaccinationAdministeredStockDeductionHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<VaccinationAdministeredStockDeductionHandler> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(VaccinationAdministeredNotification wrapper, CancellationToken cancellationToken)
    {
        var notification = wrapper.DomainEvent;

        if (notification.InventoryItemId == null || notification.DosageQuantity == null || notification.DosageQuantity <= 0)
        {
            return; // Nothing to deduct
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Processing auto-stock deduction for VaccinationAdministeredEvent (EventId: {EventId}, InventoryItemId: {InventoryItemId}, Dosage: {DosageQuantity})",
                notification.VaccinationEventId, notification.InventoryItemId, notification.DosageQuantity);
        }

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(notification.InventoryItemId.Value, cancellationToken);
        
        if (medicineItem != null && medicineItem.IsActive && medicineItem.CurrentStock >= notification.DosageQuantity.Value)
        {
            var txId = Guid.NewGuid();
            medicineItem.DeductStock(notification.DosageQuantity.Value, txId);

            var tx = new StockTransaction(
                txId,
                notification.TenantId,
                medicineItem.FarmId,
                medicineItem.Id,
                StockTransactionType.AutoMedicineConsumption,
                notification.DosageQuantity.Value,
                medicineItem.WeightedAverageCostBdt,
                medicineItem.CurrentStock,
                notification.AdministeredDate,
                reason: $"Automated deduction for {notification.VaccineName} administration on animal {notification.AnimalId}",
                referenceId: notification.VaccinationEventId);

            _inventoryItemRepository.Update(medicineItem);
            await _transactionRepository.AddAsync(tx, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully deducted {DosageQuantity} from item '{ItemName}' (Remaining: {Remaining})",
                    notification.DosageQuantity, medicineItem.Name, medicineItem.CurrentStock);
            }
        }
        else if (medicineItem != null)
        {
            _logger.LogWarning("Failed to auto-deduct stock for VaccinationAdministeredEvent {EventId}: Insufficient stock (Requested: {DosageQuantity}, Available: {CurrentStock}) or item inactive.",
                notification.VaccinationEventId, notification.DosageQuantity, medicineItem.CurrentStock);
        }
    }
}
