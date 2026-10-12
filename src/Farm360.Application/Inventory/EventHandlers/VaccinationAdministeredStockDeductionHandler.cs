using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory;
using Farm360.Domain.Inventory.Enums;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Inventory.EventHandlers;

public sealed record VaccinationAdministeredNotification(VaccinationAdministeredEvent DomainEvent) : INotification;

/// <summary>
/// Deducts the vaccine dose from stock and books its cost against Inventory. If the dose was not in
/// stock it must have been obtained directly, so its value is booked as a cash cost instead.
/// Postings are keyed on the vaccination event, so a re-published event cannot double-book.
/// </summary>
public sealed class VaccinationAdministeredStockDeductionHandler :
    INotificationHandler<VaccinationAdministeredNotification>,
    INotificationHandler<VaccinationAdministeredEvent>
{
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IStockTransactionRepository _transactionRepository;
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaccinationAdministeredStockDeductionHandler> _logger;

    public VaccinationAdministeredStockDeductionHandler(
        IInventoryItemRepository inventoryItemRepository,
        IStockTransactionRepository transactionRepository,
        ILedgerPostingService ledgerPosting,
        IUnitOfWork unitOfWork,
        ILogger<VaccinationAdministeredStockDeductionHandler> logger)
    {
        _inventoryItemRepository = inventoryItemRepository;
        _transactionRepository = transactionRepository;
        _ledgerPosting = ledgerPosting;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(VaccinationAdministeredNotification wrapper, CancellationToken cancellationToken) =>
        Handle(wrapper.DomainEvent, cancellationToken);

    public async Task Handle(VaccinationAdministeredEvent notification, CancellationToken cancellationToken)
    {
        if (notification.InventoryItemId == null || notification.DosageQuantity == null || notification.DosageQuantity <= 0)
        {
            return; // Nothing to deduct
        }

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(notification.InventoryItemId.Value, cancellationToken);
        if (medicineItem == null)
            return;

        var dose = notification.DosageQuantity.Value;
        var description = $"Vaccination - {notification.VaccineName}";

        if (medicineItem.IsActive && medicineItem.CurrentStock >= dose)
        {
            var txId = Guid.NewGuid();
            medicineItem.DeductStock(dose, txId);

            await _transactionRepository.AddAsync(new StockTransaction(
                txId,
                notification.TenantId,
                medicineItem.FarmId,
                medicineItem.Id,
                StockTransactionType.AutoMedicineConsumption,
                dose,
                medicineItem.WeightedAverageCostBdt,
                medicineItem.CurrentStock,
                notification.AdministeredDate,
                reason: $"Automated deduction for {notification.VaccineName} administration on animal {notification.AnimalId}",
                referenceId: notification.VaccinationEventId), cancellationToken);

            _inventoryItemRepository.Update(medicineItem);

            await _ledgerPosting.PostConsumptionAsync(
                medicineItem, dose, medicineItem.WeightedAverageCostBdt, notification.AdministeredDate,
                notification.VaccinationEventId, description, TransactionCategory.MedicineCost,
                animalId: notification.AnimalId, cancellationToken: cancellationToken);
        }
        else
        {
            _logger.LogWarning("Insufficient stock to auto-deduct for vaccination {EventId} (Requested: {DosageQuantity}, Available: {CurrentStock}); booking the dose as a direct cash cost.",
                notification.VaccinationEventId, dose, medicineItem.CurrentStock);

            await _ledgerPosting.PostAsync(new LedgerPosting(
                notification.TenantId, medicineItem.FarmId, TransactionCategory.MedicineCost,
                dose * medicineItem.WeightedAverageCostBdt, notification.AdministeredDate.ToDateTime(TimeOnly.MinValue),
                notification.VaccinationEventId, "Health", description,
                Notes: $"{dose:0.###} {medicineItem.UnitOfMeasure} of {medicineItem.Name} not available in stock — obtained directly",
                AnimalId: notification.AnimalId), cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
