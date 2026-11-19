using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Jobs;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Livestock;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Jobs;

/// <summary>
/// Creates each active plan's feeding entries — the nightly job, or "Generate entries" for a day
/// that was missed. Days already generated are skipped, so running it again is safe. With
/// <paramref name="FarmId"/> only that farm's plans are used.
/// </summary>
/// <remarks>
/// <para>From a request (a tenant is set): runs for that tenant only.</para>
/// <para>
/// From the recurring job (no tenant): fans out per tenant through <see cref="ITenantJobRunner"/>,
/// each tenant in its own scope, so one tenant's bad plan cannot fail everyone else's day.
/// </para>
/// <para>
/// With no <paramref name="Date"/>, every day the tenant missed since its last generated day is
/// caught up (at most the last <see cref="GenerateTenantDailyFeedingEntriesCommand.MaxCatchUpDays"/>
/// days), "today" being the tenant's local date (<see cref="IFarmClock"/>).
/// </para>
/// </remarks>
public sealed record CreateDailyFeedingEntriesCommand(DateOnly? Date = null, Guid? FarmId = null) : IRequest;

public sealed class CreateDailyFeedingEntriesCommandHandler(
    ITenantService tenant,
    ISender sender,
    ITenantJobRunner runner,
    ILogger<CreateDailyFeedingEntriesCommandHandler> logger) : IRequestHandler<CreateDailyFeedingEntriesCommand>
{
    public const string JobName = "daily-feeding-entries";

    public async Task Handle(CreateDailyFeedingEntriesCommand request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId != Guid.Empty)
        {
            await sender.Send(new GenerateTenantDailyFeedingEntriesCommand(tenant.TenantId, request.Date, request.FarmId, ThrowOnFailure: true), cancellationToken);
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Creating daily feeding entries for every tenant");

        await runner.ForEachTenantAsync(
            JobName,
            (scope, ct) => scope.Sender.Send(new GenerateTenantDailyFeedingEntriesCommand(scope.TenantId, request.Date, request.FarmId), ct),
            cancellationToken);
    }
}

/// <summary>Result of one tenant's run.</summary>
public sealed record DailyFeedingGenerationResult(int EntriesCreated, int FarmDaysFailed, IReadOnlyList<DateOnly> Days);

/// <summary>
/// One tenant's daily feeding entries: works out the days to generate (the requested day, or the
/// missed days since the last generated one), then generates each farm's day in its own
/// transaction (<see cref="GenerateFarmDailyFeedingEntriesCommand"/>). One farm-day failing is
/// logged and the rest carry on; a concurrent run that already wrote the day counts as done.
/// </summary>
public sealed record GenerateTenantDailyFeedingEntriesCommand(
    Guid TenantId,
    DateOnly? Date = null,
    Guid? FarmId = null,
    bool ThrowOnFailure = false) : IRequest<DailyFeedingGenerationResult>
{
    /// <summary>The automatic catch-up never reaches back further than this many days (today included).</summary>
    public const int MaxCatchUpDays = 7;

    /// <summary>A manual "generate for a missed day" may reach back this far.</summary>
    public const int MaxManualDaysBack = 90;
}

public sealed class GenerateTenantDailyFeedingEntriesCommandHandler(
    IAnimalFeedingPlanRepository plans,
    IDailyFeedingEntryRepository entries,
    IFarmClock clock,
    ISender sender,
    ILogger<GenerateTenantDailyFeedingEntriesCommandHandler> logger,
    IAnimalFeedAllocationRepository? allocations = null)
    : IRequestHandler<GenerateTenantDailyFeedingEntriesCommand, DailyFeedingGenerationResult>
{
    public async Task<DailyFeedingGenerationResult> Handle(GenerateTenantDailyFeedingEntriesCommand request, CancellationToken cancellationToken)
    {
        var today = request.FarmId is { } onlyFarm
            ? clock.TodayForFarm(request.TenantId, onlyFarm)
            : clock.TodayFor(request.TenantId);

        var days = await DaysToGenerateAsync(request, today, cancellationToken);

        var farmIds = (await plans.GetActivePlansAsync(request.TenantId, cancellationToken))
            .Select(p => p.FarmId)
            .Where(f => request.FarmId is null || f == request.FarmId)
            .Distinct()
            .ToList();

        var created = 0;
        var failed = 0;
        Exception? firstFailure = null;

        foreach (var farmId in farmIds)
        {
            await StopFeedingExitedAnimalsAsync(farmId, cancellationToken);

            foreach (var day in days)
            {
                try
                {
                    created += await sender.Send(new GenerateFarmDailyFeedingEntriesCommand(request.TenantId, farmId, day), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (DatabaseErrors.IsUniqueViolation(ex))
                {
                    // Another run (a manual "generate" racing the nightly job) wrote this farm-day first.
                    if (logger.IsEnabled(LogLevel.Information))
                        logger.LogInformation("Feeding entries for farm {FarmId} on {Date} were already generated by a concurrent run.", farmId, day);
                }
#pragma warning disable CA1031 // One farm-day's failure must not stop the tenant's other farms and days.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    failed++;
                    firstFailure ??= ex;
                    logger.LogError(ex, "Generating feeding entries failed for tenant {TenantId}, farm {FarmId}, {Date}; other farms continue.",
                        request.TenantId, farmId, day);
                }
            }
        }

        if (request.ThrowOnFailure && firstFailure is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(firstFailure).Throw();
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("Tenant {TenantId}: {Created} feeding entries created for {Days} across {Farms} farms ({Failed} farm-days failed).",
                request.TenantId, created, string.Join(", ", days), farmIds.Count, failed);

        return new DailyFeedingGenerationResult(created, failed, days);
    }

    /// <summary>
    /// Self-repair: a sold/dead animal whose feeding was never stopped (sold before exits were
    /// applied to feeding, or a failed exit) still has active plans or feed allocated after it left.
    /// Its exit is applied here, so it disappears from feeding and its cost stops at the exit day
    /// without anyone having to run a repair. Each animal in its own transaction; a failure is
    /// logged and retried on the next run.
    /// </summary>
    private async Task StopFeedingExitedAnimalsAsync(Guid farmId, CancellationToken cancellationToken)
    {
        if (allocations is null)
            return;

        foreach (var (animalId, exitDate, sold) in await allocations.GetExitedAnimalsStillFedAsync(farmId, cancellationToken))
        {
            try
            {
                var result = await sender.Send(new ApplyAnimalExitToFeedingCommand(animalId, exitDate, sold), cancellationToken);
                if (result.ChangedAnything && logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation("Stopped feeding for exited animal {AnimalId} from {ExitDate}: {Plans} plans ended, {Skipped} entries skipped, {Reversed} reversed, {Resplit} group entries re-split.",
                        animalId, exitDate, result.PlansCancelled, result.EntriesSkipped, result.EntriesReversed, result.GroupEntriesResplit);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One animal's repair must not stop the day's feeding entries.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Could not stop feeding for exited animal {AnimalId} (farm {FarmId}); will retry next run.", animalId, farmId);
            }
        }
    }

    private async Task<IReadOnlyList<DateOnly>> DaysToGenerateAsync(
        GenerateTenantDailyFeedingEntriesCommand request, DateOnly today, CancellationToken cancellationToken)
    {
        if (request.Date is { } requested)
        {
            if (requested > today)
                throw new ArgumentException("Feeding entries can't be created for a future day.");
            if (requested < today.AddDays(-GenerateTenantDailyFeedingEntriesCommand.MaxManualDaysBack))
                throw new ArgumentException($"Missed days can be caught up for the last {GenerateTenantDailyFeedingEntriesCommand.MaxManualDaysBack} days only.");
            return [requested];
        }

        // Automatic catch-up: every day after the last generated one, at most a week back.
        var earliest = today.AddDays(1 - GenerateTenantDailyFeedingEntriesCommand.MaxCatchUpDays);
        var last = await entries.GetLastGeneratedEntryDateAsync(request.TenantId, cancellationToken);
        var from = last is { } l && l >= earliest ? l.AddDays(1) : earliest;
        if (last is null || from > today)
        {
            from = today; // first run ever, or already up to date: (re)check today only — new plans may have started.
        }

        var days = new List<DateOnly>();
        for (var d = from; d <= today; d = d.AddDays(1))
            days.Add(d);
        return days;
    }
}

/// <summary>
/// One farm's feeding entries for one day, in one transaction. Idempotent: (plan, rule line, day)
/// keys already present are skipped, and the unique index on that key stops a concurrent duplicate.
/// Returns the number of entries created.
/// </summary>
public sealed record GenerateFarmDailyFeedingEntriesCommand(Guid TenantId, Guid FarmId, DateOnly Date) : IRequest<int>, ITransactionalCommand;

public sealed class GenerateFarmDailyFeedingEntriesCommandHandler : IRequestHandler<GenerateFarmDailyFeedingEntriesCommand, int>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IFeedFormulaRepository _formulaRepository;
    private readonly IFeedIngredientRepository _feedIngredientRepository;
    private readonly Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository _inventoryRepository;
    private readonly IFeedAllocationService _allocationService;
    private readonly IAnimalFeedAllocationRepository _allocationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateFarmDailyFeedingEntriesCommandHandler> _logger;
    private readonly Farm360.Application.Inventory.Units.IUnitService? _units;

    public GenerateFarmDailyFeedingEntriesCommandHandler(
        IAnimalFeedingPlanRepository planRepository,
        IFeedingRuleSetRepository ruleSetRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalRepository animalRepository,
        IFeedFormulaRepository formulaRepository,
        IFeedIngredientRepository feedIngredientRepository,
        Farm360.Domain.Inventory.Interfaces.Repositories.IInventoryItemRepository inventoryRepository,
        IFeedAllocationService allocationService,
        IAnimalFeedAllocationRepository allocationRepository,
        IUnitOfWork unitOfWork,
        ILogger<GenerateFarmDailyFeedingEntriesCommandHandler> logger,
        Farm360.Application.Inventory.Units.IUnitService? units = null)
    {
        _units = units;
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _entryRepository = entryRepository;
        _animalRepository = animalRepository;
        _formulaRepository = formulaRepository;
        _feedIngredientRepository = feedIngredientRepository;
        _inventoryRepository = inventoryRepository;
        _allocationService = allocationService;
        _allocationRepository = allocationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<int> Handle(GenerateFarmDailyFeedingEntriesCommand request, CancellationToken cancellationToken)
    {
        var day = request.Date;
        var activePlans = await _planRepository.GetActivePlansByFarmAsync(request.TenantId, request.FarmId, cancellationToken);
        if (activePlans.Count == 0)
            return 0;

        var existingKeys = await _entryRepository.GetEntryKeysAsync(request.TenantId, request.FarmId, day, cancellationToken);

        // Loaded once per farm-day instead of once per plan / rule line.
        var ruleSets = new Dictionary<Guid, FeedingRuleSet?>();
        var unitCosts = new Dictionary<Guid, decimal>();
        var animalIds = activePlans.Where(p => p.AnimalId.HasValue).Select(p => p.AnimalId!.Value).Distinct().ToList();
        var animals = animalIds.Count == 0
            ? new Dictionary<Guid, Animal>()
            : (await _animalRepository.GetByIdsAsync(animalIds, cancellationToken)).ToDictionary(a => a.Id);

        // Live cost per kg of each recipe: stock items priced per kg of feed (a premix kept in grams
        // costs per gram x 1000), percentages normalised by the recipe's total.
        var calculator = new FeedCostCalculator(_feedIngredientRepository, _inventoryRepository, _units);

        async Task<decimal> ResolveUnitCostAsync(Guid formulaId)
        {
            if (unitCosts.TryGetValue(formulaId, out var cached))
                return cached;
            var formula = await _formulaRepository.GetByIdAsync(formulaId, cancellationToken);
            var cost = Math.Round(await calculator.FormulaCostPerKgAsync(formula, cancellationToken), 4);
            unitCosts[formulaId] = cost;
            return cost;
        }

        var created = 0;
        var allocatedAnimals = 0;

        foreach (var plan in activePlans)
        {
            // Skip plans where the day is outside the plan's validity window
            if (day < plan.StartDate || (plan.EndDate.HasValue && day > plan.EndDate.Value))
                continue;

            // Skip plans that have an exclusion for the day
            // (a multi-day pause covers every day from its start until it resumes)
            if (plan.IsPausedOn(day))
                continue;

            if (!ruleSets.TryGetValue(plan.FeedingRuleSetId, out var ruleSet))
            {
                ruleSet = await _ruleSetRepository.GetByIdAcrossTenantsAsync(plan.FeedingRuleSetId, cancellationToken);
                ruleSets[plan.FeedingRuleSetId] = ruleSet;
            }

            // Skip plan if the feeding rule set does not exist or is inactive
            if (ruleSet == null || !ruleSet.IsActive)
                continue;

            decimal currentWeight = plan.TriggeredByWeightKg ?? 0;
            int? ageDays = null;

            if (plan.AnimalId.HasValue)
            {
                if (!animals.TryGetValue(plan.AnimalId.Value, out var animal))
                {
                    animal = await _animalRepository.GetByIdAcrossTenantsAsync(plan.AnimalId.Value, cancellationToken);
                }

                // Defence in depth: an animal is fed on a day only if it is on the farm that day
                // (acquired on or before it, not yet sold or dead on it — AnimalFeedingPresence).
                // Its plan should already be closed by the exit, but a sale or death recorded late,
                // or a catch-up run for an earlier day, must never generate feed for it.
                if (animal is not null && !AnimalFeedingPresence.IsFedOn(animal, day))
                    continue;

                if (animal?.LatestWeightKg != null && animal.LatestWeightKg.Value > 0)
                {
                    currentWeight = animal.LatestWeightKg.Value;
                }
                if (animal is not null)
                {
                    ageDays = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, day);
                }
            }

            var match = FeedingRuleMatcher.Match(ruleSet, currentWeight, ageDays);
            var matchingRules = match.Lines.ToList();

            // Outside every range the rule set defines: the nearest range's ration is used, and
            // said out loud -- an underweight animal risks over-feeding, an oversized one under-feeding.
            if (match.Fit != RuleMatchFit.InRange && _logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Plan {PlanId} (animal {AnimalId}): {Measure} is {Fit} rule set {RuleSetId} ({PlanType}); using the nearest range's ration -- verify the animal's record and the rule set's coverage.",
                    plan.Id, plan.AnimalId, match.Measure, match.Fit, ruleSet.Id, ruleSet.PlanType);
            }

            // Keep the plan's cached rule and triggered weight updated if changed
            var primaryRule = matchingRules.FirstOrDefault();
            if (primaryRule != null && plan.AnimalId.HasValue && currentWeight > 0 && plan.TriggeredByWeightKg != currentWeight)
            {
                var expKg = FeedingRuleMatcher.ExpectedKg(ruleSet, primaryRule, currentWeight);
                plan.UpdateCurrentRule(primaryRule.Id, currentWeight, expKg, primaryRule.RoughageKgPerDay);
                _planRepository.Update(plan);
            }

            foreach (var ruleLine in matchingRules)
            {
                if (!existingKeys.Add((plan.Id, ruleLine.Id)))
                    continue; // already generated (or a duplicate line in this run)

                var expectedKg = FeedingRuleMatcher.ExpectedKg(ruleSet, ruleLine, currentWeight);

                var entry = new DailyFeedingEntry(
                    id: Guid.NewGuid(),
                    tenantId: plan.TenantId,
                    feedingPlanId: plan.Id,
                    farmId: plan.FarmId,
                    entryDate: day,
                    formulaId: ruleLine.FormulaId,
                    expectedKg: expectedKg,
                    ruleLineId: ruleLine.Id,
                    shedId: plan.ShedId,
                    penId: plan.PenId,
                    batchId: plan.BatchId
                );

                decimal unitCost = await ResolveUnitCostAsync(ruleLine.FormulaId);
                entry.SetConsumptionCost(unitCost);

                await _entryRepository.AddAsync(entry, cancellationToken);
                created++;

                // Fan the plan-level entry out to the animals it actually feeds (docs/32 GAP-1).
                // Written here, at the moment feed is recorded, because herd composition and
                // weights move: deriving the split later would silently change past reports.
                var allocations = await _allocationService.BuildAllocationsAsync(entry, plan, isBackfill: false, cancellationToken);
                if (allocations.Count > 0)
                {
                    _allocationRepository.AddRange(allocations);
                    allocatedAnimals += allocations.Count;

                    // A group plan's quantity is per head: its planned cost covers every head fed.
                    var allocatedKg = allocations.Sum(a => a.AllocatedKg);
                    if (!plan.AnimalId.HasValue && expectedKg > 0 && allocatedKg > expectedKg)
                        entry.SetConsumptionCost(unitCost, Math.Round(allocatedKg / expectedKg, 4));
                }
                else if (_logger.IsEnabled(LogLevel.Warning))
                {
                    // Feed was booked against a scope that holds no active animals. Worth saying
                    // out loud: the cost exists but no animal will ever carry it.
                    _logger.LogWarning(
                        "Feeding entry {EntryId} on plan {PlanId} allocated to no animals (batch {BatchId}, shed {ShedId}, pen {PenId}).",
                        entry.Id, plan.Id, plan.BatchId, plan.ShedId, plan.PenId);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Farm {FarmId} {Date}: {Created} feeding entries created; {AllocationCount} per-animal feed allocations written.",
                request.FarmId, day, created, allocatedAnimals);

        return created;
    }
}
