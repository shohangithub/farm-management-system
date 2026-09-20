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

            var matchingRule = ruleSet.Lines.FirstOrDefault(l =>
                notification.WeightKg >= l.WeightFromKg && (l.WeightToKg == 0 || notification.WeightKg < l.WeightToKg));

            if (matchingRule == null)
            {
                var maxWeightFrom = ruleSet.Lines.Max(l => l.WeightFromKg);
                if (notification.WeightKg >= maxWeightFrom)
                {
                    matchingRule = ruleSet.Lines.OrderByDescending(l => l.WeightFromKg).First();
                }
                else
                {
                    matchingRule = ruleSet.Lines.OrderBy(l => l.WeightFromKg).First();
                }
            }

            if (matchingRule != null)
            {
                decimal expectedKg = matchingRule.ConcentrateKgPerDay;

                if (ruleSet.PlanType == FeedingPlanType.WeightPercentage)
                {
                    expectedKg = (notification.WeightKg * matchingRule.ConcentrateKgPerDay) / 100m;
                }

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
