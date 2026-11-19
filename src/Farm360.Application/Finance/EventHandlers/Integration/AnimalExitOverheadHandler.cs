using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// When an animal is sold or dies, labour and overhead that were already split onto it for a
/// period reaching its exit date (or later) are split again. A sale or death is often recorded
/// after the month's overhead run, and dated back; without this the animal would keep its share
/// for the days after it left and the animals still on the farm would be under-charged.
/// Re-splitting uses the normal head-day rule, so the animal keeps its share up to the day before
/// it left. Idempotent: running it twice re-splits to the same result.
/// </summary>
public sealed class AnimalExitOverheadHandler(
    IAnimalOverheadAllocationRepository allocations,
    IOverheadAllocationService overhead) :
    INotificationHandler<AnimalSoldNotification>,
    INotificationHandler<AnimalDiedEvent>
{
    public Task Handle(AnimalSoldNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return ResplitAsync(notification.DomainEvent.AnimalId, notification.DomainEvent.SaleDate, cancellationToken);
    }

    public Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return ResplitAsync(notification.AnimalId, notification.DeathDate, cancellationToken);
    }

    private async Task ResplitAsync(Guid animalId, DateOnly exitDate, CancellationToken cancellationToken)
    {
        var rows = await allocations.GetByAnimalAsync(animalId, exitDate, DateOnly.MaxValue, cancellationToken);
        foreach (var transactionId in rows.Select(r => r.SourceTransactionId).Distinct().ToList())
        {
            await overhead.ReallocateTransactionAsync(transactionId, cancellationToken);
        }
    }
}
