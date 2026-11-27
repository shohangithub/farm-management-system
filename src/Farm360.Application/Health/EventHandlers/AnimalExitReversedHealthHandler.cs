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
    IFarmClock? clock = null,
    Farm360.Domain.Livestock.Repositories.IAnimalRepository? animals = null,
    IFarmBusinessRulesProvider? rules = null) : INotificationHandler<AnimalExitReversedEvent>
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

        var doses = await vaccinations.GetEventsByAnimalIdAsync(notification.AnimalId, cancellationToken);
        foreach (var dose in doses)
        {
            if (dose.ReopenAfterExitUndone(reason, today))
            {
                vaccinations.UpdateEvent(dose);
                changed = true;
            }
        }

        // A repeating task whose last dose was given/skipped but has no next one (the chain stopped
        // because the animal had left) starts again: next due date from that dose, never before today.
        var animal = animals is null ? null : await animals.GetByIdAsync(notification.AnimalId, cancellationToken);
        if (animal is not null && !animal.HasLeftFarm)
        {
            var weight = Farm360.Application.Health.Tasks.DoseWeight.For(
                animal.LatestWeightKg, rules?.For(notification.TenantId, notification.FarmId).FallbackAnimalWeightKg ?? 150m);
            var known = doses.ToList();
            foreach (var closed in doses.Where(d => d.IsRecurring && d.ClosedOn is not null).ToList())
            {
                var next = Farm360.Application.Health.Tasks.HealthTaskScheduling.NextOccurrence(closed, animal, known, weight, notBefore: today);
                if (next is null) continue;
                vaccinations.AddEvent(next);
                known.Add(next);
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
