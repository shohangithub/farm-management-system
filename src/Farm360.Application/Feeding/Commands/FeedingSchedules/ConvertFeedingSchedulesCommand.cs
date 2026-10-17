using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Commands.FeedingSchedules;

public sealed record ConvertFeedingSchedulesResult(int Converted, int Skipped, IReadOnlyList<string> Notes);

/// <summary>
/// Turns the farm's active feeding schedules into group feeding plans. A schedule only described
/// feeding — it never produced daily entries, took stock or charged cost — so each becomes a
/// direct ration (its recipe, its kg per head) on a plan for the same shed, pen or batch, which
/// does all three. The schedule is then switched off; its record is kept.
/// </summary>
public sealed record ConvertFeedingSchedulesCommand(Guid FarmId) : IRequest<ConvertFeedingSchedulesResult>, ITransactionalCommand;

public sealed class ConvertFeedingSchedulesCommandHandler(
    IFeedingScheduleRepository schedules,
    IAnimalFeedingPlanRepository plans,
    IDirectFeedService directFeed,
    ITenantService tenant,
    IUnitOfWork unitOfWork) : IRequestHandler<ConvertFeedingSchedulesCommand, ConvertFeedingSchedulesResult>
{
    public async Task<ConvertFeedingSchedulesResult> Handle(ConvertFeedingSchedulesCommand request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var notes = new List<string>();
        int converted = 0, skipped = 0;

        var active = (await schedules.GetListByFarmAsync(tenant.TenantId, request.FarmId, cancellationToken))
            .Where(s => s.IsActive)
            .ToList();
        var existing = await plans.GetActivePlansByFarmAsync(tenant.TenantId, request.FarmId, cancellationToken);

        foreach (var schedule in active)
        {
            if (schedule.EndDate is { } end && end < today)
            {
                schedule.SetActiveStatus(false);
                schedules.Update(schedule);
                notes.Add($"\"{schedule.Title}\" had already ended — switched off.");
                skipped++;
                continue;
            }

            if (schedule.ShedId is null && schedule.PenId is null && schedule.BatchId is null)
            {
                notes.Add($"\"{schedule.Title}\" is not linked to a shed, pen or batch — set its ration on the Who Eats What page instead.");
                skipped++;
                continue;
            }

            // Ad-lib schedules have no fixed amount; their kg is kept as the planned amount.
            var ruleSet = await directFeed.GetOrCreateDirectRuleSetAsync(
                new DirectRation(schedule.FormulaId, null, schedule.TargetQuantityKgPerHead),
                TargetAnimalType.Cattle,
                cancellationToken);

            var duplicate = existing.Any(p => p.AnimalId is null && p.FeedingRuleSetId == ruleSet.Id
                                              && p.ShedId == schedule.ShedId && p.PenId == schedule.PenId && p.BatchId == schedule.BatchId);
            if (!duplicate)
            {
                // From today: earlier days were never fed through the system and are not back-filled.
                var start = schedule.StartDate > today ? schedule.StartDate : today;
                await plans.AddAsync(new AnimalFeedingPlan(
                    Guid.NewGuid(), tenant.TenantId, request.FarmId, ruleSet.Id, FeedingPlanType.FixedQuantity,
                    start, schedule.EndDate, null, schedule.BatchId, schedule.ShedId, schedule.PenId), cancellationToken);
            }

            schedule.SetActiveStatus(false);
            schedules.Update(schedule);
            converted++;
            notes.Add($"\"{schedule.Title}\" → {schedule.TargetQuantityKgPerHead:0.###} kg/head/day from {(schedule.StartDate > today ? schedule.StartDate : today):dd MMM yyyy}.");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ConvertFeedingSchedulesResult(converted, skipped, notes);
    }
}
