using Farm360.Application.Feeding.Services;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Feeding.EventHandlers;

/// <summary>
/// A sold animal stops being fed from its sale date: its plans end, pending entries from that day
/// are skipped, confirmed ones after it reversed, and group entries re-split without it
/// (<see cref="IAnimalExitService"/>, idempotent). The sell command calls the service directly;
/// this handler covers anything else that publishes the event.
/// </summary>
public sealed class AnimalSoldEventHandler(IAnimalExitService exitService) : INotificationHandler<AnimalSoldEvent>
{
    public async Task Handle(AnimalSoldEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await exitService.ProcessExitAsync(notification.AnimalId, notification.SaleDate,
            AnimalExitService.SoldReason(notification.SaleDate), cancellationToken);
    }
}
