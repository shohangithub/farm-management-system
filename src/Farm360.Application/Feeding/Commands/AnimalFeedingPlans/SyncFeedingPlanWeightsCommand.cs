using Farm360.Application.Feeding.Services;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public sealed record SyncFeedingPlanWeightsCommand(
    Guid? FarmId = null,
    Guid? AnimalId = null) : IRequest<SyncFeedingPlanWeightsResultDto>;

public sealed record SyncFeedingPlanWeightsResultDto(
    int TotalActivePlans,
    int UpdatedPlans,
    int UpdatedEntriesCount);

public sealed class SyncFeedingPlanWeightsCommandHandler : IRequestHandler<SyncFeedingPlanWeightsCommand, SyncFeedingPlanWeightsResultDto>
{
    private readonly IFarmClock _clock;

    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;
    private readonly ILogger<SyncFeedingPlanWeightsCommandHandler> _logger;

    public SyncFeedingPlanWeightsCommandHandler(
        IAnimalFeedingPlanRepository planRepository,
        IFeedingRuleSetRepository ruleSetRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ITenantService tenantService,
        ILogger<SyncFeedingPlanWeightsCommandHandler> logger,
        IFarmClock? clock = null)
    {
        _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _entryRepository = entryRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _tenantService = tenantService;
        _logger = logger;
    }

    public async Task<SyncFeedingPlanWeightsResultDto> Handle(SyncFeedingPlanWeightsCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;
        var today = _clock.Today;

        IReadOnlyList<Domain.Feeding.AnimalFeedingPlan> plans;
        if (request.AnimalId.HasValue)
        {
            plans = await _planRepository.GetActivePlansForAnimalAsync(tenantId, request.AnimalId.Value, cancellationToken);
        }
        else if (request.FarmId.HasValue)
        {
            plans = await _planRepository.GetActivePlansByFarmAsync(tenantId, request.FarmId.Value, cancellationToken);
        }
        else
        {
            plans = await _planRepository.GetActivePlansAsync(tenantId, cancellationToken);
        }

        int updatedPlans = 0;
        int updatedEntries = 0;

        foreach (var plan in plans)
        {
            if (!plan.AnimalId.HasValue)
                continue;

            var animal = await _animalRepository.GetByIdAsync(plan.AnimalId.Value, cancellationToken);
            if (animal?.LatestWeightKg == null || animal.LatestWeightKg <= 0)
                continue;

            var latestWeight = animal.LatestWeightKg.Value;

            var ruleSet = await _ruleSetRepository.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken);
            if (ruleSet == null || !ruleSet.IsActive || ruleSet.Lines.Count == 0)
                continue;

            var ageDays = FeedingRuleMatcher.AgeInDays(animal.DateOfBirth, _clock.Today);
            var match = FeedingRuleMatcher.Match(ruleSet, latestWeight, ageDays);
            var matchingRule = match.Primary;

            if (matchingRule == null)
                continue;

            var expectedKg = FeedingRuleMatcher.ExpectedKg(ruleSet, matchingRule, latestWeight);

            bool planChanged = plan.TriggeredByWeightKg != latestWeight
                || plan.CurrentRuleLineId != matchingRule.Id
                || plan.CurrentConcentrateKgPerDay != expectedKg;

            if (planChanged)
            {
                plan.UpdateCurrentRule(
                    matchingRule.Id,
                    latestWeight,
                    expectedKg,
                    matchingRule.RoughageKgPerDay);

                _planRepository.Update(plan);
                updatedPlans++;
            }

            // Sync today's pending daily feeding entries
            // Each entry follows the band's line for its own recipe (a band can feed concentrate AND
            // roughage); collapsing them onto the primary line would break the (plan, line, day) key.
            var pendingEntries = await _entryRepository.GetPendingEntriesByPlanIdAndDateAsync(tenantId, plan.Id, today, cancellationToken);
            var usedLines = new HashSet<Guid>();
            foreach (var entry in pendingEntries)
            {
                var line = match.Lines.FirstOrDefault(l => l.FormulaId == entry.FormulaId && !usedLines.Contains(l.Id));
                if (line is null)
                    continue;

                usedLines.Add(line.Id);
                var lineKg = FeedingRuleMatcher.ExpectedKg(ruleSet, line, latestWeight);
                if (entry.ExpectedKg != lineKg || entry.RuleLineId != line.Id)
                {
                    entry.UpdateExpectedQuantity(lineKg, line.Id);
                    _entryRepository.Update(entry);
                    updatedEntries++;
                }
            }
        }

        if (updatedPlans > 0 || updatedEntries > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Synced {UpdatedPlans} feeding plans and {UpdatedEntries} entries to latest animal weights",
                    updatedPlans, updatedEntries);
            }
        }

        return new SyncFeedingPlanWeightsResultDto(
            TotalActivePlans: plans.Count,
            UpdatedPlans: updatedPlans,
            UpdatedEntriesCount: updatedEntries);
    }
}
