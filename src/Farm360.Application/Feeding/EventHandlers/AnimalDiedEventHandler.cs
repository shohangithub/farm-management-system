using Farm360.Application.Feeding.Services;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Feeding.EventHandlers;

/// <summary>
/// A dead animal stops being fed from its death date: its plans end, pending entries from that day
/// are skipped, confirmed ones after it reversed, and group entries re-split without it
/// (<see cref="IAnimalExitService"/>, idempotent, saves its own work).
/// </summary>
public sealed class AnimalDiedEventHandler(IAnimalExitService exitService) : INotificationHandler<AnimalDiedEvent>
{
    public async Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        await exitService.ProcessExitAsync(notification.AnimalId, notification.DeathDate,
            AnimalExitService.DiedReason(notification.DeathDate), cancellationToken);
    }
}
