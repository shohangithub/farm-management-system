using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;

namespace Farm360.Application.Health.EventHandlers;

/// <summary>
/// A sale or death was undone: doses and treatment courses that <see cref="AnimalExitHealthHandler"/>
/// closed because of it are reopened (a dose whose date has passed becomes Overdue).
/// </summary>
public sealed class AnimalExitReversedHealthHandler(
    IVaccinationRepository vaccinations,
    IMedicalTreatmentRepository treatments,
    IUnitOfWork unitOfWork,
    IFarmClock? clock = null) : INotificationHandler<AnimalExitReversedEvent>
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task Handle(AnimalExitReversedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var reason = notification.WasSold
            ? AnimalExitHealthHandler.SoldReason(notification.ExitDate)
            : AnimalExitHealthHandler.DiedReason(notification.ExitDate);
        var today = _clock.TodayForFarm(notification.TenantId, notification.FarmId);
        var changed = false;

        foreach (var dose in await vaccinations.GetEventsByAnimalIdAsync(notification.AnimalId, cancellationToken))
        {
            if (dose.ReopenAfterExitUndone(reason, today))
            {
                vaccinations.UpdateEvent(dose);
                changed = true;
            }
        }

        foreach (var treatment in await treatments.GetByAnimalIdAsync(notification.AnimalId, cancellationToken))
        {
            if (treatment.ReopenAfterExitUndone(reason))
            {
                treatments.Update(treatment);
                changed = true;
            }
        }

        if (changed)
            await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
