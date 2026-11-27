using Farm360.Domain.Health;
using Farm360.Domain.Livestock;

namespace Farm360.Application.Health.Tasks;

/// <summary>The weight a dose is worked out from: the animal's latest weight, else the farm's fallback (estimated).</summary>
public readonly record struct DoseWeight(decimal WeightKg, bool Estimated)
{
    public static DoseWeight For(decimal? latestWeightKg, decimal fallbackWeightKg) =>
        latestWeightKg is > 0
            ? new DoseWeight(latestWeightKg.Value, false)
            : new DoseWeight(fallbackWeightKg > 0 ? fallbackWeightKg : 150m, true);
}

/// <summary>Turns a protocol into dated health tasks for an animal.</summary>
public static class HealthTaskScheduling
{
    /// <summary>
    /// One task per protocol step, dated <c>startDate + step day</c>, with any weight-based dose worked
    /// out from <paramref name="weight"/>. With <paramref name="skipPastOneOffsBefore"/> (automatic start
    /// for an animal registered late) one-off steps dated before it are left out, and a repeating step
    /// dated before it is moved to <paramref name="today"/> so its chain still starts.
    /// </summary>
    public static IReadOnlyList<VaccinationEvent> BuildEvents(
        VaccinationProtocol protocol,
        Guid tenantId,
        Guid animalId,
        DateOnly startDate,
        DoseWeight weight,
        DateOnly? skipPastOneOffsBefore = null,
        DateOnly? today = null)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        var events = new List<VaccinationEvent>();

        foreach (var step in protocol.Steps.OrderBy(s => s.TargetAgeDays).ThenBy(s => s.StepOrder))
        {
            var date = startDate.AddDays(step.TargetAgeDays);
            if (skipPastOneOffsBefore is { } floor && date < floor)
            {
                if (step.RepeatEveryDays is not > 0) continue;
                date = today ?? floor;
            }

            var @event = VaccinationEvent.ScheduleFromStep(
                tenantId, animalId, protocol.Id, step, date,
                $"Scheduled from protocol '{protocol.Title}' - Step '{step.StepName}'");
            @event.ApplyWeight(weight.WeightKg, weight.Estimated);
            events.Add(@event);
        }

        return events;
    }

    /// <summary>
    /// The next dose of a repeating task that was just given or skipped, unless the animal has left the
    /// farm or the chain already has its next dose. Null when nothing should be scheduled.
    /// </summary>
    public static VaccinationEvent? NextOccurrence(
        VaccinationEvent closed,
        Animal? animal,
        IEnumerable<VaccinationEvent> animalEvents,
        DoseWeight weight,
        DateOnly? notBefore = null)
    {
        ArgumentNullException.ThrowIfNull(closed);
        ArgumentNullException.ThrowIfNull(animalEvents);
        if (!closed.IsRecurring || closed.ClosedOn is null)
            return null;
        // Sold or dead: the chain stops (the exit already cancelled what was pending).
        if (animal is null || animal.HasLeftFarm)
            return null;
        if (animalEvents.Any(e => e.PreviousEventId == closed.Id))
            return null;

        var next = closed.CreateNextOccurrence(notBefore);
        next?.ApplyWeight(weight.WeightKg, weight.Estimated);
        return next;
    }
}
