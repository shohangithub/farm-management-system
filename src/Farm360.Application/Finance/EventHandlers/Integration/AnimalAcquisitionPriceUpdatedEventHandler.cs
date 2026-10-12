using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalAcquisitionPriceUpdatedNotification(AnimalAcquisitionPriceUpdatedEvent DomainEvent) : INotification;

/// <summary>
/// Keeps the purchase posting, the cost ledger and — for animals already sold or dead — the cost
/// released from the livestock asset in step with a corrected price or acquisition date.
/// </summary>
public sealed class AnimalAcquisitionPriceUpdatedEventHandler :
    INotificationHandler<AnimalAcquisitionPriceUpdatedNotification>,
    INotificationHandler<AnimalAcquisitionPriceUpdatedEvent>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly ILivestockAccountingService _livestockAccounting;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnimalAcquisitionPriceUpdatedEventHandler> _logger;

    public AnimalAcquisitionPriceUpdatedEventHandler(
        IAnimalCostLedgerRepository ledgerRepository,
        IAnimalRepository animalRepository,
        ILivestockAccountingService livestockAccounting,
        IUnitOfWork unitOfWork,
        ILogger<AnimalAcquisitionPriceUpdatedEventHandler> logger)
    {
        _ledgerRepository = ledgerRepository;
        _animalRepository = animalRepository;
        _livestockAccounting = livestockAccounting;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public Task Handle(AnimalAcquisitionPriceUpdatedNotification notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(AnimalAcquisitionPriceUpdatedEvent notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification, cancellationToken);

    private async Task ProcessEventAsync(AnimalAcquisitionPriceUpdatedEvent domainEvent, CancellationToken cancellationToken)
    {
        var newPrice = domainEvent.NewPriceBdt ?? 0m;

        var ledger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger != null)
        {
            ledger.UpdateAcquisitionCost(newPrice);
            _ledgerRepository.Update(ledger);
        }
        else
        {
            _ledgerRepository.Add(AnimalCostLedger.Create(domainEvent.TenantId, domainEvent.AnimalId, domainEvent.FarmId, newPrice));
        }

        var animal = await _animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
        if (animal is not null)
            await _livestockAccounting.SyncPurchaseAsync(animal, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Synchronized animal purchase price update for Animal {AnimalId} (Old: {OldPrice} BDT, New: {NewPrice} BDT)",
                domainEvent.AnimalId, domainEvent.OldPriceBdt, domainEvent.NewPriceBdt);
        }
    }
}
