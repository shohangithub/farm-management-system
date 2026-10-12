using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalRegisteredNotification(AnimalRegisteredEvent DomainEvent) : INotification;

/// <summary>
/// Opens the animal's cost ledger and books its purchase as a Livestock asset (Dr Livestock,
/// Cr Cash) dated on the acquisition date. Born-on-farm animals carry no purchase cost.
/// </summary>
public sealed class AnimalRegisteredEventHandler :
    INotificationHandler<AnimalRegisteredNotification>,
    INotificationHandler<AnimalRegisteredEvent>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly ILivestockAccountingService _livestockAccounting;
    private readonly IUnitOfWork _unitOfWork;

    public AnimalRegisteredEventHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository,
        ILivestockAccountingService livestockAccounting,
        IUnitOfWork unitOfWork)
    {
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
        _livestockAccounting = livestockAccounting;
        _unitOfWork = unitOfWork;
    }

    public Task Handle(AnimalRegisteredNotification notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(AnimalRegisteredEvent notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification, cancellationToken);

    private async Task ProcessEventAsync(AnimalRegisteredEvent domainEvent, CancellationToken cancellationToken)
    {
        var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);

        var existingLedger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (existingLedger == null)
        {
            var acquisition = animal?.AcquisitionType == AcquisitionType.BornOnFarm
                ? 0m
                : domainEvent.AcquisitionPriceBdt ?? 0m;
            _ledgerRepository.Add(AnimalCostLedger.Create(domainEvent.TenantId, domainEvent.AnimalId, domainEvent.FarmId, acquisition));
        }

        if (animal is not null)
            await _livestockAccounting.SyncPurchaseAsync(animal, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
