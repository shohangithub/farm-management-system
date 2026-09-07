using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Jobs;

public sealed record CreateDailyFeedingEntriesCommand() : IRequest;

public sealed class CreateDailyFeedingEntriesCommandHandler : IRequestHandler<CreateDailyFeedingEntriesCommand>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IAnimalRepository _animalRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateDailyFeedingEntriesCommandHandler> _logger;

    public CreateDailyFeedingEntriesCommandHandler(
        IAnimalFeedingPlanRepository planRepository,
        IFeedingRuleSetRepository ruleSetRepository,
        IDailyFeedingEntryRepository entryRepository,
        IAnimalRepository animalRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateDailyFeedingEntriesCommandHandler> logger)
    {
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _entryRepository = entryRepository;
        _animalRepository = animalRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CreateDailyFeedingEntriesCommand request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Creating daily feeding entries across all tenants");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        
        var activePlans = await _planRepository.GetAllActivePlansAcrossTenantsAsync(cancellationToken);
        var existingPlanIds = await _entryRepository.GetEntryPlanIdsAcrossTenantsByDateAsync(today, cancellationToken);
        var ruleSets = new Dictionary<Guid, FeedingRuleSet>();

        foreach (var plan in activePlans)
        {
            if (!ruleSets.TryGetValue(plan.FeedingRuleSetId, out var ruleSet))
            {
                var fetchedRuleSet = await _ruleSetRepository.GetByIdAcrossTenantsAsync(plan.FeedingRuleSetId, cancellationToken);
                if (fetchedRuleSet != null)
                {
                    ruleSets[plan.FeedingRuleSetId] = fetchedRuleSet;
                    ruleSet = fetchedRuleSet;
                }
            }

            if (ruleSet == null) continue;

            decimal currentWeight = plan.TriggeredByWeightKg ?? 0;

            var matchingRules = ruleSet.Lines
                .Where(l => currentWeight >= l.WeightFromKg && currentWeight < l.WeightToKg)
                .ToList();

            if (matchingRules.Count == 0 && ruleSet.Lines.Count > 0)
            {
                var minWeight = ruleSet.Lines.Min(l => l.WeightFromKg);
                matchingRules = ruleSet.Lines.Where(l => l.WeightFromKg == minWeight).ToList();
            }

            foreach (var ruleLine in matchingRules)
            {
                if (existingPlanIds.Contains((plan.Id, ruleLine.Id)))
                    continue;

                decimal expectedKg = ruleLine.ConcentrateKgPerDay;

                if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
                {
                    expectedKg = (currentWeight * ruleLine.ConcentrateKgPerDay) / 100m;
                }

                var entry = new DailyFeedingEntry(
                    id: Guid.NewGuid(),
                    tenantId: plan.TenantId,
                    feedingPlanId: plan.Id,
                    farmId: plan.FarmId,
                    entryDate: today,
                    formulaId: ruleLine.FormulaId,
                    expectedKg: expectedKg,
                    ruleLineId: ruleLine.Id,
                    shedId: plan.ShedId,
                    penId: plan.PenId,
                    batchId: plan.BatchId
                );

                await _entryRepository.AddAsync(entry, cancellationToken);
            }
        }
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
