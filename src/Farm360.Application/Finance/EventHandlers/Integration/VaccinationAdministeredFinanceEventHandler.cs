using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Repositories;
using Farm360.Application.Inventory.EventHandlers;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed class VaccinationAdministeredFinanceEventHandler : 
    INotificationHandler<VaccinationAdministeredNotification>,
    INotificationHandler<VaccinationAdministeredEvent>
{
    private readonly IFinancialTransactionRepository _transactionRepository;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<VaccinationAdministeredFinanceEventHandler> _logger;

    public VaccinationAdministeredFinanceEventHandler(
        IFinancialTransactionRepository transactionRepository,
        IAnimalCostLedgerRepository ledgerRepository,
        IInventoryItemRepository inventoryItemRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ILogger<VaccinationAdministeredFinanceEventHandler> logger)
    {
        _transactionRepository = transactionRepository;
        _ledgerRepository = ledgerRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(VaccinationAdministeredNotification wrapper, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(wrapper.DomainEvent, cancellationToken);
    }

    public Task Handle(VaccinationAdministeredEvent notification, CancellationToken cancellationToken)
    {
        return ProcessEventAsync(notification, cancellationToken);
    }

    private async Task ProcessEventAsync(VaccinationAdministeredEvent domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.InventoryItemId == null || domainEvent.DosageQuantity == null || domainEvent.DosageQuantity <= 0)
            return;

        var medicineItem = await _inventoryItemRepository.GetByIdAsync(domainEvent.InventoryItemId.Value, cancellationToken);
        if (medicineItem == null || medicineItem.WeightedAverageCostBdt <= 0)
            return;

        var totalCost = Math.Round(domainEvent.DosageQuantity.Value * medicineItem.WeightedAverageCostBdt, 2);
        if (totalCost <= 0)
            return;

        var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
        var farmId = animal?.FarmId ?? medicineItem.FarmId;

        // 1. Post MedicineCost expense transaction to General Ledger
        var transaction = FinancialTransaction.Create(
            tenantId: domainEvent.TenantId,
            farmId: farmId,
            type: TransactionType.Expense,
            category: TransactionCategory.MedicineCost,
            amountBdt: totalCost,
            transactionDate: domainEvent.AdministeredDate.ToDateTime(TimeOnly.MinValue),
            referenceId: domainEvent.VaccinationEventId.ToString(),
            notes: $"Vaccination: {domainEvent.VaccineName} ({domainEvent.DosageQuantity.Value:0.##} {medicineItem.UnitOfMeasure} @ {medicineItem.WeightedAverageCostBdt:0.##} BDT)",
            description: $"Vaccination - {domainEvent.VaccineName}",
            animalId: domainEvent.AnimalId,
            isAutomated: true,
            sourceModule: "Health"
        );

        await _transactionRepository.AddAsync(transaction, cancellationToken);

        // 2. Accumulate in AnimalCostLedger running total
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Posted automated vaccination expense of {Cost} BDT for Animal {AnimalId}", totalCost, domainEvent.AnimalId);
        }
    }
}
