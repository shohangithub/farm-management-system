using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Services;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedSheets;

/// <summary>
/// Removes one feed an animal was recorded as getting on a day. This is how a daily feed sheet is
/// corrected: remove the wrong line, then add the right one. A confirmed entry is fully reversed:
/// stock is returned at the cost it was issued at, the ledger posting is removed and the animal's
/// feed cost is re-set. A pending entry is skipped. Entries for a whole group (a shed or batch
/// plan) can't be removed here because they belong to every animal in the group.
/// </summary>
public sealed record RemoveFeedSheetEntryCommand(Guid FarmId, Guid EntryId, string? Reason = null) : IRequest, ITransactionalCommand;

public sealed class RemoveFeedSheetEntryCommandValidator : AbstractValidator<RemoveFeedSheetEntryCommand>
{
    public RemoveFeedSheetEntryCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.EntryId).NotEmpty();
        RuleFor(x => x.Reason).MaximumLength(150);
    }
}

public sealed class RemoveFeedSheetEntryCommandHandler(
    IDailyFeedingEntryRepository entries,
    IAnimalFeedingPlanRepository plans,
    IAnimalFeedAllocationRepository allocations,
    IAnimalCostLedgerRepository ledgers,
    IStockTransactionRepository stockTransactions,
    IInventoryItemRepository inventory,
    ILedgerPostingService ledgerPosting,
    IFarmClock clock,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<RemoveFeedSheetEntryCommand>
{
    public async Task Handle(RemoveFeedSheetEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await entries.GetByIdAsync(request.EntryId, cancellationToken);
        if (entry is null || entry.FarmId != request.FarmId)
            throw new NotFoundException(nameof(DailyFeedingEntry), request.EntryId);

        var plan = await plans.GetByIdAsync(entry.FeedingPlanId, cancellationToken);
        if (plan?.AnimalId is null)
            throw new DomainException("This feed was given to a whole group by its feeding plan, so it can't be removed for one animal.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Removed from the daily feed sheet" : request.Reason.Trim();

        switch (entry.Status)
        {
            case DailyFeedingEntryStatus.Pending:
                await sender.Send(new SkipDailyFeedingEntryCommand(entry.Id, reason), cancellationToken);
                return;

            case DailyFeedingEntryStatus.Confirmed:
            case DailyFeedingEntryStatus.Adjusted:
                var touched = new HashSet<Guid> { plan.AnimalId.Value };
                await ConfirmedFeedEntryReversal.ReverseAsync(
                    entry, reason, touched, stockTransactions, inventory, ledgerPosting, entries, allocations, clock.Today, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                await AnimalFeedCostSync.SyncAsync(allocations, ledgers, entry.TenantId, entry.FarmId, touched, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return;

            default:
                throw new ConflictException("This feed was already removed or skipped.");
        }
    }
}

public sealed record EndFeedingPlansResult(int PlansEnded, IReadOnlyList<string> GroupPlansStillCovering);

/// <summary>
/// Switches animals to daily-sheet feeding by ending their own (individual) feeding plans today, so
/// the Today page stops creating planned entries for them. Group plans (a shed, pen or batch) are
/// not changed, because other animals depend on them. Their names are returned so the user can
/// pause or cancel them on the plans page.
/// </summary>
public sealed record EndFeedingPlansForAnimalsCommand(Guid FarmId, IReadOnlyList<Guid> AnimalIds)
    : IRequest<EndFeedingPlansResult>, ITransactionalCommand;

public sealed class EndFeedingPlansForAnimalsCommandValidator : AbstractValidator<EndFeedingPlansForAnimalsCommand>
{
    public EndFeedingPlansForAnimalsCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.AnimalIds).NotEmpty().WithMessage("Choose at least one animal.");
    }
}

public sealed class EndFeedingPlansForAnimalsCommandHandler(
    IAnimalFeedingPlanRepository plans,
    IFeedingRuleSetRepository ruleSets,
    IAnimalRepository animals,
    ITenantService tenant,
    IFarmClock clock,
    IUnitOfWork unitOfWork) : IRequestHandler<EndFeedingPlansForAnimalsCommand, EndFeedingPlansResult>
{
    public async Task<EndFeedingPlansResult> Handle(EndFeedingPlansForAnimalsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.AnimalIds.ToHashSet();
        var today = clock.Today;
        var active = await plans.GetActivePlansByFarmAsync(tenant.TenantId, request.FarmId, cancellationToken);

        var ended = 0;
        var groups = new List<string>();
        foreach (var plan in active)
        {
            if (plan.AnimalId is { } animalId)
            {
                if (!ids.Contains(animalId))
                    continue;
                plan.Cancel(today);
                plans.Update(plan);
                ended++;
                continue;
            }

            var members = await animals.GetActiveByScopeAcrossTenantsAsync(
                tenant.TenantId, plan.BatchId, plan.ShedId, plan.PenId, today, cancellationToken);
            if (members.Any(m => ids.Contains(m.Id)))
            {
                var ruleSet = await ruleSets.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken);
                groups.Add(ruleSet?.Name ?? "Group plan");
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new EndFeedingPlansResult(ended, groups.Distinct().ToList());
    }
}
