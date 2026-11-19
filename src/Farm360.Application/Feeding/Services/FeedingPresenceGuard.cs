using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;

namespace Farm360.Application.Feeding.Services;

/// <summary>
/// Refuses to record feed for an animal that was not on the farm on the entry's date
/// (<see cref="AnimalFeedingPresence"/>): not yet acquired, or already sold or dead — the exit day
/// included. Group entries need no guard: their membership already leaves such animals out.
/// </summary>
public static class FeedingPresenceGuard
{
    /// <summary>Throws a <see cref="DomainException"/> when the entry's individual animal was not fed on its date.</summary>
    public static async Task EnsureAnimalOnFarmAsync(
        DailyFeedingEntry entry,
        IAnimalFeedingPlanRepository? plans,
        IAnimalRepository? animals,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (plans is null || animals is null)
            return;

        var plan = await plans.GetByIdAsync(entry.FeedingPlanId, cancellationToken).ConfigureAwait(false);
        if (plan?.AnimalId is not { } animalId)
            return;

        var animal = await animals.GetByIdAsync(animalId, cancellationToken).ConfigureAwait(false);
        if (animal is null)
            return;

        var reason = AnimalFeedingPresence.NotFedReason(animal, entry.EntryDate);
        if (reason is not null)
            throw new DomainException(reason);
    }
}
