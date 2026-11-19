using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.EventHandlers.Integration;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Health.EventHandlers;

/// <summary>
/// When an animal leaves the farm (sold or died) its future health work is closed:
///   - pending vaccinations / deworming doses (Scheduled or Overdue) are Cancelled with a note;
///   - ongoing treatment courses are closed at the exit date (no more doses; withdrawal alerts stop
///     because they only list animals on the farm).
/// History is kept: administered doses, finished treatments, costs and mortality records are not
/// touched (farm rules: see <c>AnimalStatusGroups</c>). Idempotent — only pending items change —
/// and saves its own changes.
///
/// Wiring: the sell command publishes <see cref="AnimalSoldNotification"/> (not
/// <see cref="AnimalSoldEvent"/>, which Finance would double-book), and both death paths publish
/// <see cref="AnimalDiedEvent"/>. Running twice for one exit is harmless.
/// </summary>
public sealed class AnimalExitHealthHandler(
    IVaccinationRepository vaccinationRepository,
    IMedicalTreatmentRepository treatmentRepository,
    IUnitOfWork unitOfWork) :
    INotificationHandler<AnimalSoldNotification>,
    INotificationHandler<AnimalDiedEvent>
{
    public Task Handle(AnimalSoldNotification notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var sold = notification.DomainEvent;
        return CloseFutureHealthWorkAsync(sold.AnimalId, sold.SaleDate, SoldReason(sold.SaleDate), cancellationToken);
    }

    public Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return CloseFutureHealthWorkAsync(notification.AnimalId, notification.DeathDate, DiedReason(notification.DeathDate), cancellationToken);
    }

    public static string SoldReason(DateOnly saleDate) => $"Closed: animal sold on {saleDate:dd MMM yyyy}";

    public static string DiedReason(DateOnly deathDate) => $"Closed: animal died on {deathDate:dd MMM yyyy}";

    private async Task CloseFutureHealthWorkAsync(Guid animalId, DateOnly exitDate, string reason, CancellationToken cancellationToken)
    {
        var changed = false;

        var vaccinations = await vaccinationRepository.GetEventsByAnimalIdAsync(animalId, cancellationToken);
        foreach (var vaccination in vaccinations)
        {
            if (vaccination.CancelForAnimalExit(exitDate, reason))
            {
                vaccinationRepository.UpdateEvent(vaccination);
                changed = true;
            }
        }

        var treatments = await treatmentRepository.GetByAnimalIdAsync(animalId, cancellationToken);
        foreach (var treatment in treatments)
        {
            if (treatment.CloseForAnimalExit(exitDate, reason))
            {
                treatmentRepository.Update(treatment);
                changed = true;
            }
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
