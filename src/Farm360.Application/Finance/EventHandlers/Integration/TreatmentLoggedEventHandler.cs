using System;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Enums;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record TreatmentLoggedNotification(TreatmentLoggedEvent DomainEvent, Guid FarmId) : INotification;

/// <summary>
/// Books the cash side of a treatment and the animal's cost ledger.
/// </summary>
/// <remarks>
/// When the treatment drew medicine from stock, that medicine is booked by the stock deduction
/// handler at stock value — booking the user-entered cost again here was the old double count.
/// Only what was charged beyond the medicine's stock value (vet fee, extra drugs bought) is a
/// cash cost here. With no stock link, the entered cost is the medicine bought for cash.
/// </remarks>
public sealed class TreatmentLoggedEventHandler :
    INotificationHandler<TreatmentLoggedNotification>,
    INotificationHandler<TreatmentLoggedEvent>
{
    private readonly ILedgerPostingService _ledgerPosting;
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TreatmentLoggedEventHandler> _logger;

    public TreatmentLoggedEventHandler(
        ILedgerPostingService ledgerPosting,
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository,
        IInventoryItemRepository inventoryItemRepository,
        IUnitOfWork unitOfWork,
        ILogger<TreatmentLoggedEventHandler> logger)
    {
        _ledgerPosting = ledgerPosting;
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(TreatmentLoggedNotification notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification.DomainEvent, notification.FarmId, cancellationToken);

    public Task Handle(TreatmentLoggedEvent notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification, Guid.Empty, cancellationToken);

    private async Task ProcessEventAsync(TreatmentLoggedEvent domainEvent, Guid farmId, CancellationToken cancellationToken)
    {
        if (farmId == Guid.Empty)
        {
            var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
            farmId = animal?.FarmId ?? Guid.Empty;
        }

        if (farmId == Guid.Empty)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("Cannot post treatment expense: FarmId could not be determined for Animal {AnimalId}", domainEvent.AnimalId);
            return;
        }

        // Value of the medicine drawn from stock (weighted average cost does not change on issue,
        // so this is the same figure the stock handler books, whichever runs first).
        var stockValue = 0m;
        var fromStock = domainEvent.InventoryItemId.HasValue && domainEvent.ConsumptionQuantity is > 0;
        if (fromStock)
        {
            var item = await _inventoryItemRepository.GetByIdAsync(domainEvent.InventoryItemId!.Value, cancellationToken);
            fromStock = item is not null;
            stockValue = item is null ? 0m : Math.Round(domainEvent.ConsumptionQuantity!.Value * item.WeightedAverageCostBdt, 2);
        }

        var date = domainEvent.StartDate.ToDateTime(TimeOnly.MinValue);
        if (fromStock)
        {
            await _ledgerPosting.PostAsync(new LedgerPosting(
                domainEvent.TenantId, farmId, TransactionCategory.VeterinaryCost,
                domainEvent.CostBdt - stockValue, date, domainEvent.MedicalTreatmentId, "Health",
                $"Treatment charges - {domainEvent.Diagnosis}",
                Notes: $"Charges beyond {domainEvent.MedicationName} taken from stock",
                AnimalId: domainEvent.AnimalId), cancellationToken);
        }
        else
        {
            await _ledgerPosting.PostAsync(new LedgerPosting(
                domainEvent.TenantId, farmId, TransactionCategory.MedicineCost,
                domainEvent.CostBdt, date, domainEvent.MedicalTreatmentId, "Health",
                $"Medical Treatment - Drug: {domainEvent.MedicationName}",
                Notes: $"Treatment: {domainEvent.MedicationName} for {domainEvent.Diagnosis}",
                AnimalId: domainEvent.AnimalId), cancellationToken);
        }

        // Management costing: the animal carries the full treatment cost.
        var animalCost = Math.Max(domainEvent.CostBdt, stockValue);
        if (animalCost > 0)
        {
            var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
            if (ledger == null)
            {
                ledger = AnimalCostLedger.Create(domainEvent.TenantId, domainEvent.AnimalId, farmId, 0m);
                ledger.RecordCost(TransactionCategory.MedicineCost, animalCost);
                _ledgerRepository.Add(ledger);
            }
            else
            {
                ledger.RecordCost(TransactionCategory.MedicineCost, animalCost);
                _ledgerRepository.Update(ledger);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
