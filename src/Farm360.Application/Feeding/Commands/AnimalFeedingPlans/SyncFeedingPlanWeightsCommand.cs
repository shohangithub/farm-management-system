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
        ILogger<SyncFeedingPlanWeightsCommandHandler> logger)
    {
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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

            var matchingRule = ruleSet.Lines.FirstOrDefault(l =>
                latestWeight >= l.WeightFromKg && (l.WeightToKg == 0 || latestWeight < l.WeightToKg));

            if (matchingRule == null)
            {
                var maxWeightFrom = ruleSet.Lines.Max(l => l.WeightFromKg);
                if (latestWeight >= maxWeightFrom)
                {
                    matchingRule = ruleSet.Lines.OrderByDescending(l => l.WeightFromKg).First();
                }
                else
                {
                    matchingRule = ruleSet.Lines.OrderBy(l => l.WeightFromKg).First();
                }
            }

            if (matchingRule == null)
                continue;

            decimal expectedKg = matchingRule.ConcentrateKgPerDay;
            if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
            {
                expectedKg = (latestWeight * matchingRule.ConcentrateKgPerDay) / 100m;
            }

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
            var pendingEntries = await _entryRepository.GetPendingEntriesByPlanIdAndDateAsync(tenantId, plan.Id, today, cancellationToken);
            foreach (var entry in pendingEntries)
            {
                if (entry.ExpectedKg != expectedKg || entry.RuleLineId != matchingRule.Id)
                {
                    entry.UpdateExpectedQuantity(expectedKg, matchingRule.Id);
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
