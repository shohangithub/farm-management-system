using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Health.EventHandlers;

/// <summary>
/// Deducts the medicine a treatment used from stock and books its cost. The medicine posting is
/// owned here because only this handler knows whether the stock was really there:
/// <list type="bullet">
/// <item>Deducted from stock — MedicineCost settled against Inventory (the cash left at purchase).</item>
/// <item>Not enough stock — the medicine evidently came from outside stock, so its value is a cash cost.</item>
/// </list>
/// Charges above the medicine's stock value are booked by the finance treatment handler.
/// </summary>
public sealed class TreatmentLoggedStockDeductionHandler : INotificationHandler<TreatmentLoggedEvent>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _stockTransactionRepository;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TreatmentLoggedStockDeductionHandler> _logger;

    public TreatmentLoggedStockDeductionHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository stockTransactionRepository,
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork,
        ILogger<TreatmentLoggedStockDeductionHandler> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _stockTransactionRepository = stockTransactionRepository;
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(TreatmentLoggedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.InventoryItemId == null || notification.ConsumptionQuantity == null || notification.ConsumptionQuantity <= 0)
        {
            return;
        }

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(notification.InventoryItemId.Value, cancellationToken);
        if (medicineItem == null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Inventory item {InventoryItemId} not found. Cannot deduct stock for treatment {MedicalTreatmentId}.",
                    notification.InventoryItemId, notification.MedicalTreatmentId);
            return;
        }

        var quantity = notification.ConsumptionQuantity.Value;
        var unitCost = medicineItem.WeightedAverageCostBdt;
        var description = $"Medical Treatment - Drug: {notification.MedicationName}";

        if (medicineItem.IsActive && medicineItem.CurrentStock >= quantity)
        {
            var stockTxId = Guid.NewGuid();
            medicineItem.DeductStock(quantity, notification.MedicalTreatmentId);
            _inventoryItemRepository.Update(medicineItem);

            await _stockTransactionRepository.AddAsync(new Domain.Inventory.StockTransaction(
                id: stockTxId,
                tenantId: notification.TenantId,
                farmId: medicineItem.FarmId,
                inventoryItemId: medicineItem.Id,
                transactionType: StockTransactionType.AutoMedicineConsumption,
                quantity: quantity,
                unitCostBdt: unitCost,
                balanceAfter: medicineItem.CurrentStock,
                transactionDate: notification.StartDate,
                reason: $"Auto-deduction for medical treatment on animal {notification.AnimalId}",
                referenceId: notification.MedicalTreatmentId), cancellationToken);

            await _ledgerPosting.PostConsumptionAsync(
                medicineItem, quantity, unitCost, notification.StartDate, notification.MedicalTreatmentId,
                description, TransactionCategory.MedicineCost, animalId: notification.AnimalId,
                cancellationToken: cancellationToken);
        }
        else
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Insufficient stock of {ItemName} for treatment {MedicalTreatmentId} (needed {Quantity}, available {Stock}); booking it as a direct cash cost.",
                    medicineItem.Name, notification.MedicalTreatmentId, quantity, medicineItem.CurrentStock);

            await _ledgerPosting.PostAsync(new LedgerPosting(
                notification.TenantId, medicineItem.FarmId, TransactionCategory.MedicineCost,
                quantity * unitCost, notification.StartDate.ToDateTime(TimeOnly.MinValue),
                notification.MedicalTreatmentId, "Health", description,
                Notes: $"{quantity:0.###} {medicineItem.UnitOfMeasure} of {medicineItem.Name} not available in stock — bought directly",
                AnimalId: notification.AnimalId), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
