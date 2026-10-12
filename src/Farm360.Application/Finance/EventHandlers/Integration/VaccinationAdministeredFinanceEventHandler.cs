using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Adds a vaccination's cost to the animal's cost ledger (management costing).
/// </summary>
/// <remarks>
/// The ledger posting itself is made by <see cref="VaccinationAdministeredStockDeductionHandler"/>,
/// which knows whether the dose really came out of stock. Posting it here as well was a double count.
/// </remarks>
public sealed class VaccinationAdministeredFinanceEventHandler :
    INotificationHandler<VaccinationAdministeredNotification>,
    INotificationHandler<VaccinationAdministeredEvent>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;

    public VaccinationAdministeredFinanceEventHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IInventoryItemRepository inventoryItemRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork)
    {
        _ledgerRepository = ledgerRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
    }

    public Task Handle(VaccinationAdministeredNotification wrapper, CancellationToken cancellationToken) =>
        ProcessEventAsync(wrapper.DomainEvent, cancellationToken);

    public Task Handle(VaccinationAdministeredEvent notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification, cancellationToken);

    private async Task ProcessEventAsync(VaccinationAdministeredEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.InventoryItemId == null || domainEvent.DosageQuantity is not > 0)
            return;

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(domainEvent.InventoryItemId.Value, cancellationToken);
        if (medicineItem == null || medicineItem.WeightedAverageCostBdt <= 0)
            return;

        var totalCost = Math.Round(domainEvent.DosageQuantity.Value * medicineItem.WeightedAverageCostBdt, 2);
        if (totalCost <= 0)
            return;

        var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
        var farmId = animal?.FarmId ?? medicineItem.FarmId;

        var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger == null)
        {
            ledger = AnimalCostLedger.Create(domainEvent.TenantId, domainEvent.AnimalId, farmId, 0m);
            ledger.RecordCost(TransactionCategory.MedicineCost, totalCost);
            _ledgerRepository.Add(ledger);
        }
        else
        {
            ledger.RecordCost(TransactionCategory.MedicineCost, totalCost);
            _ledgerRepository.Update(ledger);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
