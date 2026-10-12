using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Livestock.Events;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Finance.EventHandlers.Integration;

/// <summary>
/// Writes a dead animal's purchase cost off the livestock asset as a mortality loss (or, for a
/// slaughter, as the cost of the animal disposed). Without this the asset kept animals that no
/// longer exist and the loss never reached profit &amp; loss.
/// </summary>
public sealed class AnimalDiedFinanceEventHandler(
    IAnimalRepository animalRepository,
    ILivestockAccountingService livestockAccounting,
    IUnitOfWork unitOfWork) : INotificationHandler<AnimalDiedEvent>
{
    public async Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        var animal = await animalRepository.GetByIdAsync(notification.AnimalId, cancellationToken);
        if (animal is null)
            return;

        await livestockAccounting.SyncDisposalAsync(animal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
