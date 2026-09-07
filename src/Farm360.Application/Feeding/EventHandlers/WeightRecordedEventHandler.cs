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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<WeightRecordedEventHandler> _logger;

    public WeightRecordedEventHandler(
        IAnimalFeedingPlanRepository planRepository, 
        IFeedingRuleSetRepository ruleSetRepository,
        IUnitOfWork unitOfWork,
        ILogger<WeightRecordedEventHandler> logger)
    {
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(WeightRecordedEvent notification, CancellationToken cancellationToken)
    {
        var plans = await _planRepository.GetActivePlansForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (plans.Count == 0) return;

        foreach (var plan in plans)
        {
            var ruleSet = await _ruleSetRepository.GetByIdAsync(plan.FeedingRuleSetId, cancellationToken);
            if (ruleSet == null) continue;

            var matchingRule = ruleSet.Lines.FirstOrDefault(l =>
                notification.WeightKg >= l.WeightFromKg && notification.WeightKg < l.WeightToKg);

            matchingRule ??= ruleSet.Lines.OrderBy(l => l.WeightFromKg).FirstOrDefault();

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
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
