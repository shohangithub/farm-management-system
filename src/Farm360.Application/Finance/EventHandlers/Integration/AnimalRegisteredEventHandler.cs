using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Finance;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalRegisteredNotification(AnimalRegisteredEvent DomainEvent) : INotification;

public sealed class AnimalRegisteredEventHandler : INotificationHandler<AnimalRegisteredNotification>
{
    private readonly IAnimalCostLedgerRepository _ledgerRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AnimalRegisteredEventHandler(IAnimalCostLedgerRepository ledgerRepository, IUnitOfWork unitOfWork)
    {
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(AnimalRegisteredNotification notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        // 1. Check if ledger already exists (idempotency check)
        var existingLedger = await _ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (existingLedger != null)
            return;

        // 2. Create a new cost ledger for this animal
        var ledger = AnimalCostLedger.Create(
            domainEvent.TenantId,
            domainEvent.AnimalId,
            domainEvent.FarmId,
            domainEvent.AcquisitionPriceBdt ?? 0m
        );

        _ledgerRepository.Add(ledger);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
