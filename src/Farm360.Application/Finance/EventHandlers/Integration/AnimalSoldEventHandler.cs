using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

public sealed record AnimalSoldNotification(AnimalSoldEvent DomainEvent) : INotification;

/// <summary>
/// Books a sale: revenue received in cash, and the animal's purchase cost released from the
/// livestock asset as Cost of Animals Sold — so the period's profit is sale price minus cost, not
/// the full sale price. Idempotent per animal.
/// </summary>
public sealed class AnimalSoldEventHandler(
    IAnimalRepository animalRepository,
    ILivestockAccountingService livestockAccounting,
    IAnimalCostLedgerRepository ledgerRepository,
    IUnitOfWork unitOfWork) :
    INotificationHandler<AnimalSoldNotification>,
    INotificationHandler<AnimalSoldEvent>
{
    public Task Handle(AnimalSoldNotification notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification.DomainEvent, cancellationToken);

    public Task Handle(AnimalSoldEvent notification, CancellationToken cancellationToken) =>
        ProcessEventAsync(notification, cancellationToken);

    private async Task ProcessEventAsync(AnimalSoldEvent domainEvent, CancellationToken cancellationToken)
    {
        var animal = await animalRepository.GetByIdAsync(domainEvent.AnimalId, cancellationToken);
        if (animal is not null)
            await livestockAccounting.SyncDisposalAsync(animal, cancellationToken);

        // The ledger normally exists from registration; if it doesn't (older data), create it at the
        // book cost so the sale revenue is never lost and the animal's profit is shown.
        var ledger = await ledgerRepository.GetByAnimalIdAsync(domainEvent.AnimalId, cancellationToken);
        if (ledger is null && animal is not null)
        {
            ledger = Farm360.Domain.Finance.AnimalCostLedger.Create(
                animal.TenantId, animal.Id, animal.FarmId, await livestockAccounting.BookCostAsync(animal, cancellationToken));
            ledger.RecordSaleRevenue(domainEvent.SalePriceBdt);
            ledgerRepository.Add(ledger);
        }
        else if (ledger is not null)
        {
            ledger.RecordSaleRevenue(domainEvent.SalePriceBdt);
            ledgerRepository.Update(ledger);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
