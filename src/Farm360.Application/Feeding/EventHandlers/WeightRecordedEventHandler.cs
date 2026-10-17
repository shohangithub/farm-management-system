using Farm360.Application.Feeding.Services;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class WeightRecordedEventHandler : INotificationHandler<WeightRecordedEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WeightRecordedEventHandler> _logger;

    public WeightRecordedEventHandler(
        IAnimalFeedingPlanRepository planRepository, 
        IFeedingRuleSetRepository ruleSetRepository,
        IDailyFeedingEntryRepository entryRepository,
        IUnitOfWork unitOfWork,
        ILogger<WeightRecordedEventHandler> logger)
    {
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _entryRepository = entryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(WeightRecordedEvent notification, CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetActivePlansForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (plans.Count == 0) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var plan in plans)
        {
            var ruleSet = await _ruleSetRepository.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken);
            if (ruleSet == null || !ruleSet.IsActive || ruleSet.Lines.Count == 0) continue;

            // Age and fixed-quantity plans don't change with weight.
            if (ruleSet.PlanType is FeedingPlanType.AgeBased or FeedingPlanType.FixedQuantity) continue;

            var matchingRule = FeedingRuleMatcher.Match(ruleSet, notification.WeightKg, null).Primary;

            if (matchingRule != null)
            {
                var expectedKg = FeedingRuleMatcher.ExpectedKg(ruleSet, matchingRule, notification.WeightKg);

                plan.UpdateCurrentRule(
                    matchingRule.Id,
                    notification.WeightKg,
                    expectedKg,
                    matchingRule.RoughageKgPerDay
                );

                _planRepository.Update(plan);

                // Update any pending daily feeding entries for today so rations reflect the new weight immediately
                var pendingEntries = await _entryRepository.GetPendingEntriesByPlanIdAndDateAsync(notification.TenantId, plan.Id, today, cancellationToken);
                foreach (var entry in pendingEntries)
                {
                    entry.UpdateExpectedQuantity(expectedKg, matchingRule.Id);
                    _entryRepository.Update(entry);
                }
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
