using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Enums;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public sealed record ManualFeedingResult(int Recorded, IReadOnlyList<string> Notes);

/// <summary>
/// Records feed actually given to one or more animals on a day, outside their plan — an animal not
/// on any ration yet, an extra feed, or a day the plan missed. Each becomes a confirmed feeding
/// entry like any other: stock is deducted, the cost is booked and charged to the animal, and it
/// shows in every feeding report.
/// </summary>
/// <param name="FormulaId">A recipe, or…</param>
/// <param name="InventoryItemId">…a stock item fed as it is.</param>
/// <param name="QuantityPerHead">Kg per animal (for a stock item, in its own unit when that isn't kg).</param>
public sealed record RecordManualFeedingCommand(
    Guid FarmId,
    DateOnly Date,
    IReadOnlyList<Guid> AnimalIds,
    Guid? FormulaId,
    Guid? InventoryItemId,
    decimal QuantityPerHead,
    string? Notes = null) : IRequest<ManualFeedingResult>;

public sealed class RecordManualFeedingCommandValidator : AbstractValidator<RecordManualFeedingCommand>
{
    public RecordManualFeedingCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.AnimalIds).NotEmpty().WithMessage("Choose at least one animal.");
        RuleFor(x => x.QuantityPerHead).GreaterThan(0).WithMessage("Enter how much each animal was fed.");
        RuleFor(x => x).Must(x => x.FormulaId.HasValue || x.InventoryItemId.HasValue)
            .WithMessage("Choose a recipe or a stock item.");
        RuleFor(x => x.Date).Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Feed can't be recorded for a future day.");
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class RecordManualFeedingCommandHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IDailyFeedingEntryRepository entries,
    IAnimalFeedAllocationRepository allocations,
    IFeedAllocationService allocationService,
    IFeedFormulaRepository formulas,
    IDirectFeedService directFeed,
    ITenantService tenant,
    IUnitOfWork unitOfWork,
    ISender sender) : IRequestHandler<RecordManualFeedingCommand, ManualFeedingResult>
{
    public async Task<ManualFeedingResult> Handle(RecordManualFeedingCommand request, CancellationToken cancellationToken)
    {
        // The ration as a one-line fixed rule set ("Direct · <recipe or item> · <kg>/day"), reused if it exists.
        var ruleSet = await directFeed.GetOrCreateDirectRuleSetAsync(
            new DirectRation(request.InventoryItemId is null ? request.FormulaId : null, request.InventoryItemId, request.QuantityPerHead),
            TargetAnimalType.Cattle, cancellationToken);
        var line = ruleSet.Lines.First();
        var formula = await formulas.GetByIdAsync(line.FormulaId, cancellationToken);
        var unitCost = formula?.TotalCostPerKgBdt ?? 0m;
        var reason = "Manual entry" + (string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : $": {request.Notes.Trim()}");

        var notes = new List<string>();
        var created = new List<DailyFeedingEntry>();
        foreach (var animalId in request.AnimalIds.Distinct())
        {
            var animal = await animals.GetByIdAsync(animalId, cancellationToken);
            if (animal is null || animal.FarmId != request.FarmId)
            {
                notes.Add($"Animal {animalId} was not found on this farm — skipped.");
                continue;
            }
            if (animal.Status is not (AnimalStatus.Active or AnimalStatus.Quarantined))
            {
                notes.Add($"{animal.Tag.TagId} is {animal.Status} — skipped.");
                continue;
            }

            // A one-day plan holds the entry (it is closed straight away, so the daily job never repeats it).
            var plan = new AnimalFeedingPlan(Guid.NewGuid(), tenant.TenantId, request.FarmId, ruleSet.Id,
                FeedingPlanType.FixedQuantity, request.Date, request.Date, animalId);
            plan.UpdateCurrentRule(line.Id, animal.LatestWeightKg ?? 0m, request.QuantityPerHead, 0m);
            plan.Complete();
            await plans.AddAsync(plan, cancellationToken);

            var entry = new DailyFeedingEntry(Guid.NewGuid(), tenant.TenantId, plan.Id, request.FarmId, request.Date,
                line.FormulaId, request.QuantityPerHead, line.Id);
            entry.SetConsumptionCost(unitCost);
            await entries.AddAsync(entry, cancellationToken);

            var shares = await allocationService.BuildAllocationsAsync(entry, plan, isBackfill: false, cancellationToken);
            if (shares.Count > 0)
                allocations.AddRange(shares);

            created.Add(entry);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Confirm each one exactly as the Today page does: stock check, deduction, cost and ledger.
        // Short of stock: the entry stays pending on that day's Today page, to confirm after a stock-in.
        var confirmed = 0;
        foreach (var entry in created)
        {
            try
            {
                await sender.Send(new ConfirmDailyFeedingEntryCommand(entry.Id, request.QuantityPerHead, null, reason), cancellationToken);
                confirmed++;
            }
            catch (DomainException ex)
            {
                notes.Add($"Saved as pending, not confirmed: {ex.Message}");
            }
        }

        return new ManualFeedingResult(confirmed, notes);
    }
}
