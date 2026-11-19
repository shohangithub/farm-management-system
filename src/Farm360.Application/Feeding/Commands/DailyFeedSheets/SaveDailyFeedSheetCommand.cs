using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Commands.DailyFeedingEntries;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.BusinessRules;
using Farm360.Domain.Exceptions;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Allocation;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Inventory.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Feeding.Commands.DailyFeedSheets;

/// <summary>How a sheet line's quantity is meant.</summary>
public enum FeedSheetQuantityMode
{
    /// <summary>Every animal got this much.</summary>
    PerHead = 0,

    /// <summary>This much was given to the whole group; it is split between the animals.</summary>
    GroupTotal = 1,
}

/// <summary>How a group total is split between the animals.</summary>
public enum FeedSheetSplit
{
    /// <summary>The same amount for every animal.</summary>
    Equal = 0,

    /// <summary>More for heavier animals, following the farm's feed allocation rule (metabolic or live weight).</summary>
    ByWeight = 1,
}

/// <summary>One feed on the sheet: a recipe or a stock item, and how much.</summary>
/// <param name="Quantity">Kg of a recipe; for a stock item, in <paramref name="UnitId"/> (or its base unit).</param>
public sealed record FeedSheetLine(
    Guid? FormulaId,
    Guid? InventoryItemId,
    decimal Quantity,
    FeedSheetQuantityMode Mode = FeedSheetQuantityMode.PerHead,
    Guid? UnitId = null);

public sealed record DailyFeedSheetResult(
    int AnimalsFed,
    int EntriesRecorded,
    int PlannedEntriesSkipped,
    decimal TotalCostBdt,
    IReadOnlyList<string> Notes);

/// <summary>
/// Records everything a group of animals was actually fed on one day, in any mix of recipes and
/// stock items, with no feeding plan needed. All of it is saved at once: stock check, deduction,
/// cost, and the charge to each animal. If anything fails (a feed short of stock, for example),
/// nothing is saved. Pending planned entries for these animals that day are skipped, so the feed
/// is never counted twice.
/// </summary>
public sealed record SaveDailyFeedSheetCommand(
    Guid FarmId,
    DateOnly Date,
    IReadOnlyList<Guid> AnimalIds,
    IReadOnlyList<FeedSheetLine> Lines,
    FeedSheetSplit Split = FeedSheetSplit.ByWeight,
    bool SkipPlannedEntries = true,
    string? Notes = null) : IRequest<DailyFeedSheetResult>, ITransactionalCommand;

public sealed class SaveDailyFeedSheetCommandValidator : AbstractValidator<SaveDailyFeedSheetCommand>
{
    public SaveDailyFeedSheetCommandValidator(IFarmClock? clock = null)
    {
        clock ??= Farm360.Application.Common.Time.FarmClock.System;
        RuleFor(x => x.FarmId).NotEmpty();
        RuleFor(x => x.AnimalIds).NotEmpty().WithMessage("Choose at least one animal.");
        RuleFor(x => x.AnimalIds).Must(a => a is null || a.Count <= 2000).WithMessage("A sheet can cover at most 2000 animals.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one feed.");
        RuleFor(x => x.Lines).Must(l => l is null || l.Count <= 30).WithMessage("A sheet can have at most 30 feeds.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Enter a quantity for every feed.");
            line.RuleFor(l => l).Must(l => l.FormulaId.HasValue || l.InventoryItemId.HasValue)
                .WithMessage("Choose a recipe or a stock item for every feed.");
        });
        RuleFor(x => x.Date).Must(d => d <= clock.Today).WithMessage("Feed can't be recorded for a future day.");
        RuleFor(x => x.Notes).MaximumLength(200);
    }
}

public sealed class SaveDailyFeedSheetCommandHandler(
    IAnimalRepository animals,
    IAnimalFeedingPlanRepository plans,
    IDailyFeedingEntryRepository entries,
    IAnimalFeedAllocationRepository allocations,
    IFeedAllocationService allocationService,
    IFeedFormulaRepository formulas,
    IFeedIngredientRepository ingredients,
    IInventoryItemRepository inventory,
    IDirectFeedService directFeed,
    IFarmBusinessRulesProvider rules,
    ITenantService tenant,
    IFarmClock clock,
    IUnitOfWork unitOfWork,
    ISender sender,
    Farm360.Application.Inventory.Units.IUnitService? units = null)
    : IRequestHandler<SaveDailyFeedSheetCommand, DailyFeedSheetResult>
{
    public const string ReasonPrefix = "Daily feed sheet";

    public async Task<DailyFeedSheetResult> Handle(SaveDailyFeedSheetCommand request, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var reason = ReasonPrefix + (string.IsNullOrWhiteSpace(request.Notes) ? string.Empty : $": {request.Notes.Trim()}");

        // ── 1. The animals: on this farm and on the farm that day ───────────────
        var fed = new List<Animal>();
        foreach (var animal in await animals.GetByIdsAsync(request.AnimalIds.Distinct().ToList(), cancellationToken))
        {
            if (animal.FarmId != request.FarmId)
            {
                notes.Add($"{animal.Tag.TagId} is not on this farm. Skipped.");
                continue;
            }

            if (AnimalFeedingPresence.NotFedReason(animal, request.Date) is { } notOnFarm)
            {
                notes.Add($"{notOnFarm} Skipped.");
                continue;
            }

            fed.Add(animal);
        }

        if (fed.Count == 0)
            throw new DomainException("None of the chosen animals were on the farm that day, so nothing was recorded.");

        fed = fed.OrderBy(a => a.Tag.TagId, StringComparer.OrdinalIgnoreCase).ToList();
        var fedIds = fed.Select(a => a.Id).ToHashSet();

        // ── 2. Planned entries for these animals that day are replaced by the sheet ──
        var skipped = request.SkipPlannedEntries
            ? await SkipPlannedEntriesAsync(request, fedIds, reason, notes, cancellationToken)
            : 0;

        // ── 3. Work out every animal's quantity of every feed, and check stock for the lot ──
        var farmRules = rules.For(tenant.TenantId, request.FarmId);
        var prepared = new List<PreparedLine>(request.Lines.Count);
        foreach (var line in request.Lines)
            prepared.Add(await PrepareLineAsync(line, fed, request.Split, farmRules, cancellationToken));

        await EnsureStockAsync(prepared, cancellationToken);

        // ── 4. Write one confirmed entry per animal per feed ────────────────────
        var created = new List<(DailyFeedingEntry Entry, decimal Quantity)>();
        foreach (var line in prepared)
        {
            foreach (var (animal, quantity) in line.PerAnimal)
            {
                if (quantity <= 0m)
                    continue;

                // A one-day plan holds the entry. It is closed straight away, so the daily job never repeats it.
                var plan = new AnimalFeedingPlan(Guid.NewGuid(), tenant.TenantId, request.FarmId, line.RuleSet.Id,
                    FeedingPlanType.FixedQuantity, request.Date, request.Date, animal.Id);
                plan.UpdateCurrentRule(line.RuleLine.Id, animal.LatestWeightKg ?? 0m, quantity, 0m);
                plan.Complete();
                plan.MarkOneOff();
                await plans.AddAsync(plan, cancellationToken);

                var entry = new DailyFeedingEntry(Guid.NewGuid(), tenant.TenantId, plan.Id, request.FarmId, request.Date,
                    line.RuleLine.FormulaId, quantity, line.RuleLine.Id);
                entry.SetConsumptionCost(line.UnitCostBdt);
                await entries.AddAsync(entry, cancellationToken);

                var shares = await allocationService.BuildAllocationsAsync(entry, plan, isBackfill: false, cancellationToken);
                if (shares.Count > 0)
                    allocations.AddRange(shares);

                created.Add((entry, quantity));
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Confirmed exactly as the Today page does it: stock deduction, cost snapshot, ledger, animal
        // cost. This command's transaction covers all of them, so one failure undoes the whole sheet.
        foreach (var (entry, quantity) in created)
            await sender.Send(new ConfirmDailyFeedingEntryCommand(entry.Id, quantity, null, reason), cancellationToken);

        var totalCost = created.Sum(c => c.Entry.TotalCostBdt ?? 0m);
        return new DailyFeedSheetResult(fed.Count, created.Count, skipped, Math.Round(totalCost, 2), notes);
    }

    private async Task<int> SkipPlannedEntriesAsync(
        SaveDailyFeedSheetCommand request,
        HashSet<Guid> fedIds,
        string reason,
        List<string> notes,
        CancellationToken cancellationToken)
    {
        // Today's plan entries may not exist yet. Create them first so the job doesn't add them after the sheet is saved.
        if (request.Date == clock.Today)
            await sender.Send(new Farm360.Application.Feeding.Jobs.GenerateFarmDailyFeedingEntriesCommand(tenant.TenantId, request.FarmId, request.Date), cancellationToken);

        var pending = (await entries.GetEntriesByDateAsync(tenant.TenantId, request.FarmId, request.Date, cancellationToken))
            .Where(e => e.Status == DailyFeedingEntryStatus.Pending)
            .ToList();
        if (pending.Count == 0)
            return 0;

        var planById = (await plans.GetByIdsAsync(pending.Select(e => e.FeedingPlanId).Distinct().ToList(), cancellationToken))
            .ToDictionary(p => p.Id);
        var skipped = 0;
        var groupsLeft = new HashSet<Guid>();

        foreach (var entry in pending)
        {
            if (!planById.TryGetValue(entry.FeedingPlanId, out var plan))
                continue;

            bool replace;
            if (plan.AnimalId is { } animalId)
            {
                replace = fedIds.Contains(animalId);
            }
            else
            {
                // A group plan feeds its whole shed/pen/batch. Skip it only when the sheet covers every animal in it.
                var members = await animals.GetActiveByScopeAcrossTenantsAsync(
                    tenant.TenantId, plan.BatchId, plan.ShedId, plan.PenId, request.Date, cancellationToken);
                replace = members.Count > 0 && members.All(m => fedIds.Contains(m.Id));
                if (!replace && members.Any(m => fedIds.Contains(m.Id)))
                    groupsLeft.Add(plan.Id);
            }

            if (!replace)
                continue;

            await sender.Send(new SkipDailyFeedingEntryCommand(entry.Id, $"Replaced by the daily feed sheet ({reason})"), cancellationToken);
            skipped++;
        }

        if (groupsLeft.Count > 0)
            notes.Add($"{groupsLeft.Count} group plan entr{(groupsLeft.Count == 1 ? "y was" : "ies were")} left pending: " +
                      "the group includes animals that are not on this sheet. Skip it on the Today page if the whole group was fed by the sheet.");

        return skipped;
    }

    private sealed record PreparedLine(
        FeedFormula Formula,
        FeedingRuleSet RuleSet,
        FeedingRuleLine RuleLine,
        decimal UnitCostBdt,
        IReadOnlyList<(Animal Animal, decimal Quantity)> PerAnimal)
    {
        public decimal TotalQuantity => PerAnimal.Sum(p => p.Quantity);
    }

    private async Task<PreparedLine> PrepareLineAsync(
        FeedSheetLine line,
        List<Animal> fed,
        FeedSheetSplit split,
        FarmBusinessRules farmRules,
        CancellationToken cancellationToken)
    {
        var itemId = line.InventoryItemId;
        var formulaId = itemId is null ? line.FormulaId : null;

        // Quantities in the unit the user entered, per animal.
        var entered = FeedSheetSplitter.Split(fed, line.Quantity, line.Mode, split, farmRules);

        // The unit feeding records it in (kg, or a stock item's own unit such as ml): a fixed factor.
        var perEnteredUnit = (await directFeed.FeedQuantityAsync(new DirectRation(formulaId, itemId, 1m, UnitId: line.UnitId), cancellationToken)).Quantity;

        // The rule set is labelled with the average per head ("Direct · Rice straw · 6 kg/day") and reused across days.
        var averagePerHead = Math.Round(entered.Sum(e => e.Quantity) / fed.Count, 3, MidpointRounding.AwayFromZero);
        if (averagePerHead <= 0m)
            throw new DomainException("A feed quantity is too small to record.");
        var ruleSet = await directFeed.GetOrCreateDirectRuleSetAsync(
            new DirectRation(formulaId, itemId, averagePerHead, UnitId: line.UnitId), TargetAnimalType.Cattle, cancellationToken);
        var ruleLine = ruleSet.Lines.First();
        var formula = await formulas.GetByIdAsync(ruleLine.FormulaId, cancellationToken)
            ?? throw new DomainException("The feed's recipe could not be found.");

        var perAnimal = entered
            .Select(e => (e.Animal, Math.Round(e.Quantity * perEnteredUnit, 4, MidpointRounding.AwayFromZero)))
            .ToList();
        return new PreparedLine(formula, ruleSet, ruleLine, formula.TotalCostPerKgBdt, perAnimal);
    }

    /// <summary>One message listing every feed that is short, before anything is written.</summary>
    private async Task EnsureStockAsync(IReadOnlyList<PreparedLine> lines, CancellationToken cancellationToken)
    {
        var calculator = new FeedCostCalculator(ingredients, inventory, units);
        var shortfalls = new List<string>();
        foreach (var group in lines.GroupBy(l => l.Formula.Id))
        {
            var total = group.Sum(l => l.TotalQuantity);
            if (total <= 0m)
                continue;
            var requirement = await calculator.RequirementAsync(group.First().Formula, total, cancellationToken);
            shortfalls.AddRange(requirement.Shortfalls());
        }

        if (shortfalls.Count > 0)
            throw new DomainException("Not enough stock, so nothing was saved. " + string.Join(" ", shortfalls.Distinct()));
    }
}

/// <summary>Turns a sheet line's quantity into each animal's quantity.</summary>
public static class FeedSheetSplitter
{
    public static IReadOnlyList<(Animal Animal, decimal Quantity)> Split(
        IReadOnlyList<Animal> animals,
        decimal quantity,
        FeedSheetQuantityMode mode,
        FeedSheetSplit split,
        FarmBusinessRules rules)
    {
        ArgumentNullException.ThrowIfNull(animals);
        ArgumentNullException.ThrowIfNull(rules);
        if (animals.Count == 0)
            return [];

        if (mode == FeedSheetQuantityMode.PerHead)
            return animals.Select(a => (a, quantity)).ToList();

        var method = split == FeedSheetSplit.Equal
            ? FeedAllocationMethod.EqualPerHead
            : rules.FeedAllocation == FeedAllocationMethod.EqualPerHead ? FeedAllocationMethod.LiveWeight : rules.FeedAllocation;

        var shares = FeedAllocationCalculator.Allocate(
            animals.Select(a => new FeedAllocationSubject(a.Id, a.LatestWeightKg ?? 0m)).ToList(),
            quantity, 0m, method, rules.MetabolicWeightExponent, rules.FallbackAnimalWeightKg);
        var byId = shares.ToDictionary(s => s.AnimalId, s => s.AllocatedKg);
        return animals.Select(a => (a, byId.GetValueOrDefault(a.Id))).ToList();
    }
}
