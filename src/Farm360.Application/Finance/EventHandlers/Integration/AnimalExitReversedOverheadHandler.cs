using Farm360.Application.Finance.Services;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// A sale or death was undone: labour and overhead already split for periods from the old exit
/// date on are split again, so the animal (on the farm after all) takes its share of them.
/// </summary>
public sealed class AnimalExitReversedOverheadHandler(
    IAnimalOverheadAllocationRepository allocations,
    IOverheadAllocationService overhead) : INotificationHandler<AnimalExitReversedEvent>
{
    public async Task Handle(AnimalExitReversedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var rows = await allocations.GetByFarmAsync(notification.FarmId, notification.ExitDate, DateOnly.MaxValue, cancellationToken);
        foreach (var transactionId in rows.Select(r => r.SourceTransactionId).Distinct().ToList())
        {
            await overhead.ReallocateTransactionAsync(transactionId, cancellationToken);
        }
    }
}
