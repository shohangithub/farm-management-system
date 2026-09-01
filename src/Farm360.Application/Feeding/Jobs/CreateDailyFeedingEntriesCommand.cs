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
        var ruleSets = new Dictionary<Guid, FeedingRuleSet>();

        foreach (var plan in activePlans)
        {
            if (plan.CurrentRuleLineId.HasValue && plan.CurrentConcentrateKgPerDay.HasValue)
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
                
                var ruleLine = ruleSet?.Lines.FirstOrDefault(l => l.Id == plan.CurrentRuleLineId.Value);
                if (ruleLine == null) continue;

                var entry = new DailyFeedingEntry(
                    id: Guid.NewGuid(),
                    tenantId: plan.TenantId,
                    feedingPlanId: plan.Id,
                    farmId: plan.FarmId,
                    entryDate: today,
                    formulaId: ruleLine.FormulaId,
                    expectedKg: plan.CurrentConcentrateKgPerDay.Value,
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
