using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Feeding.EventHandlers;

/// <summary>
/// A sale or death was undone: the animal's own plans that the exit ended run again, and its
/// entries the exit skipped (or reversed, if they had been confirmed) go back to Pending so they
/// can be confirmed on the Confirm Planned Feed page. Shared shed/batch entries already re-divided
/// without the animal stay as they are; from the next run it is included in them again.
/// </summary>
public sealed class AnimalExitReversedFeedingHandler(
    IAnimalFeedingPlanRepository plans,
    IDailyFeedingEntryRepository entries,
    IUnitOfWork unitOfWork) : INotificationHandler<AnimalExitReversedEvent>
{
    public async Task Handle(AnimalExitReversedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var reason = notification.WasSold
            ? AnimalExitService.SoldReason(notification.ExitDate)
            : AnimalExitService.DiedReason(notification.ExitDate);
        var changed = false;

        foreach (var plan in await plans.GetAllPlansForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken))
        {
            if (plan.ReopenAfterExitUndone(notification.ExitDate))
            {
                plans.Update(plan);
                changed = true;
            }
        }

        foreach (var entry in await entries.GetEntriesByAnimalIdAsync(notification.TenantId, notification.AnimalId, cancellationToken))
        {
            if (entry.ReopenAfterExitUndone(reason))
            {
                entries.Update(entry);
                changed = true;
            }
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
