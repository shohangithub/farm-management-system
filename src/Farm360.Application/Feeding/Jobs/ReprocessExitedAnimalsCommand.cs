using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Jobs;

/// <summary>One animal the repair could not process, and why.</summary>
public sealed record ExitedAnimalSkipped(Guid AnimalId, string Tag, string Reason);

public sealed record ReprocessExitedAnimalsResult(
    int AnimalsProcessed,
    int AnimalsChanged,
    int PlansCancelled,
    int EntriesSkipped,
    int EntriesReversed,
    int GroupEntriesResplit,
    IReadOnlyList<ExitedAnimalSkipped> Skipped);

/// <summary>
/// One-off repair for feeding recorded before exits were applied to it: runs exit processing
/// (<see cref="IAnimalExitService"/>) for every sold or dead animal of the farm, from its sale or
/// death date. Idempotent — a second run changes nothing. An animal with no recorded exit date is
/// skipped and reported.
/// </summary>
public sealed record ReprocessExitedAnimalsCommand(Guid FarmId) : IRequest<ReprocessExitedAnimalsResult>;

public sealed class ReprocessExitedAnimalsCommandHandler(
    IAnimalRepository animals,
    IAnimalExitService exitService) : IRequestHandler<ReprocessExitedAnimalsCommand, ReprocessExitedAnimalsResult>
{
    public async Task<ReprocessExitedAnimalsResult> Handle(ReprocessExitedAnimalsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exited = (await animals.GetAllByFarmAsync(request.FarmId, cancellationToken))
            .Where(a => a.Status is AnimalStatus.Sold or AnimalStatus.Dead or AnimalStatus.Slaughtered)
            .OrderBy(a => a.SaleDate ?? a.DeathDate)
            .ToList();

        var skipped = new List<ExitedAnimalSkipped>();
        int processed = 0, changed = 0, plans = 0, entriesSkipped = 0, reversed = 0, resplit = 0;

        foreach (var animal in exited)
        {
            if (AnimalFeedingPresence.ExitDate(animal) is not { } exitDate)
            {
                skipped.Add(new ExitedAnimalSkipped(animal.Id, animal.Tag.TagId,
                    $"{animal.Status} but no {(animal.Status == AnimalStatus.Sold ? "sale" : "death")} date is recorded."));
                continue;
            }

            var reason = animal.SaleDate is not null
                ? AnimalExitService.SoldReason(exitDate)
                : AnimalExitService.DiedReason(exitDate);

            var result = await exitService.ProcessExitAsync(animal.Id, exitDate, reason, cancellationToken);
            processed++;
            if (result.ChangedAnything) changed++;
            plans += result.PlansCancelled;
            entriesSkipped += result.EntriesSkipped;
            reversed += result.EntriesReversed;
            resplit += result.GroupEntriesResplit;
        }

        return new ReprocessExitedAnimalsResult(processed, changed, plans, entriesSkipped, reversed, resplit, skipped);
    }
}

/// <summary>
/// Applies one sold/dead animal's exit to feeding, in its own transaction (plans ended, entries from
/// the exit day skipped or reversed with stock returned, group entries re-split, feed cost re-synced).
/// Used by the daily job to repair animals whose exit was never applied to feeding.
/// </summary>
public sealed record ApplyAnimalExitToFeedingCommand(Guid AnimalId, DateOnly ExitDate, bool Sold)
    : IRequest<AnimalExitResult>, Farm360.Application.Common.Behaviors.ITransactionalCommand;

public sealed class ApplyAnimalExitToFeedingCommandHandler(IAnimalExitService exitService)
    : IRequestHandler<ApplyAnimalExitToFeedingCommand, AnimalExitResult>
{
    public Task<AnimalExitResult> Handle(ApplyAnimalExitToFeedingCommand request, CancellationToken cancellationToken) =>
        exitService.ProcessExitAsync(request.AnimalId, request.ExitDate,
            request.Sold ? AnimalExitService.SoldReason(request.ExitDate) : AnimalExitService.DiedReason(request.ExitDate),
            cancellationToken);
}
