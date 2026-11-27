using Farm360.Application.Common.Interfaces;
using Farm360.Application.Inventory.Units;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Events;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Health.Tasks;

/// <summary>
/// Closing health tasks: giving a dose / marking a care task done, or skipping it with a reason.
/// Both work out a weight-based dose from the animal's latest weight, and both schedule the next
/// dose of a repeating task from the actual day (a late dose moves the next one too) unless the
/// animal has left the farm.
/// </summary>
public interface IHealthTaskCompletionService
{
    /// <summary>Records the tasks as given/done on <paramref name="date"/>. Stock is deducted for linked medicines.</summary>
    Task CompleteAsync(IReadOnlyCollection<Guid> eventIds, DateOnly date, string? notes, CancellationToken cancellationToken = default);

    /// <summary>Skips a task with a reason; a repeating one is scheduled again from <paramref name="date"/>.</summary>
    Task SkipAsync(Guid eventId, DateOnly date, string reason, CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IHealthTaskCompletionService"/>
public sealed class HealthTaskCompletionService(
    IVaccinationRepository vaccinations,
    IAnimalRepository animals,
    IFarmBusinessRulesProvider rules,
    ICurrentUserService currentUser,
    IUnitOfWork unitOfWork,
    IPublisher publisher,
    IFarmClock? clock = null,
    IInventoryItemRepository? inventoryItems = null,
    IUnitService? units = null) : IHealthTaskCompletionService
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task CompleteAsync(IReadOnlyCollection<Guid> eventIds, DateOnly date, string? notes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventIds);
        var userId = currentUser.UserId ?? Guid.Empty;
        var completed = new List<VaccinationEvent>();

        foreach (var id in eventIds.Distinct())
        {
            var (task, animal) = await LoadAsync(id, cancellationToken);

            // A dose given before the animal left may be entered late; none on/after the exit date.
            animal.EnsureCanRecordOn(date, task.IsCareTask ? "a health task" : "a vaccination");

            var weight = WeightOf(animal);
            task.ApplyWeight(weight.WeightKg, weight.Estimated);
            var stockQuantity = await StockQuantityAsync(task, cancellationToken);

            task.RecordAdministration(date, userId, notes, _clock.TodayForFarm(task.TenantId, animal.FarmId), stockQuantity);
            vaccinations.UpdateEvent(task);
            completed.Add(task);

            await ScheduleNextAsync(task, animal, weight, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Stock deduction and its cost posting run off this event.
        foreach (var administered in completed.SelectMany(e => e.DomainEvents).OfType<VaccinationAdministeredEvent>().ToList())
        {
            await publisher.Publish(administered, cancellationToken);
        }
    }

    public async Task SkipAsync(Guid eventId, DateOnly date, string reason, CancellationToken cancellationToken = default)
    {
        var (task, animal) = await LoadAsync(eventId, cancellationToken);
        task.Skip(date, reason, _clock.TodayForFarm(task.TenantId, animal.FarmId));
        vaccinations.UpdateEvent(task);

        await ScheduleNextAsync(task, animal, WeightOf(animal), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<(VaccinationEvent Task, Animal Animal)> LoadAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var task = await vaccinations.GetEventByIdAsync(eventId, cancellationToken)
            ?? throw new KeyNotFoundException($"Health task '{eventId}' was not found.");
        var animal = await animals.GetByIdAsync(task.AnimalId, cancellationToken)
            ?? throw new KeyNotFoundException($"The animal for this health task ({task.AnimalId}) was not found.");
        return (task, animal);
    }

    private DoseWeight WeightOf(Animal animal) =>
        DoseWeight.For(animal.LatestWeightKg, rules.For(animal.TenantId, animal.FarmId).FallbackAnimalWeightKg);

    private async Task ScheduleNextAsync(VaccinationEvent closed, Animal animal, DoseWeight weight, CancellationToken cancellationToken)
    {
        if (!closed.IsRecurring) return;

        var history = await vaccinations.GetEventsByAnimalIdAsync(closed.AnimalId, cancellationToken);
        var next = HealthTaskScheduling.NextOccurrence(closed, animal, history, weight);
        if (next is not null)
            vaccinations.AddEvent(next);
    }

    /// <summary>
    /// The dose in the linked item's base unit. A weight-based dose counted in a pack/other unit of the
    /// item (the step's stock unit) is converted with the item's unit conversions.
    /// </summary>
    private async Task<decimal?> StockQuantityAsync(VaccinationEvent task, CancellationToken cancellationToken)
    {
        if (task.IsCareTask || task.InventoryItemId is not { } itemId || task.DosageQuantity is not { } dose)
            return null;
        if (task.DoseUnitId is not { } unitId || inventoryItems is null || units is null)
            return dose;

        var item = await inventoryItems.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
            return dose;

        try
        {
            return (await units.ToBaseAsync(item, unitId, dose, 0m, cancellationToken)).BaseQuantity;
        }
        catch (ArgumentException)
        {
            // The unit no longer belongs to the item: fall back to the dose as entered.
            return dose;
        }
    }
}
