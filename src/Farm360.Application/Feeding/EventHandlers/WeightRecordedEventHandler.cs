using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class WeightRecordedEventHandler : INotificationHandler<WeightRecordedEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingRuleSetRepository _ruleSetRepository;
    private readonly ILogger<WeightRecordedEventHandler> _logger;

    public WeightRecordedEventHandler(
        IAnimalFeedingPlanRepository planRepository, 
        IFeedingRuleSetRepository ruleSetRepository,
        ILogger<WeightRecordedEventHandler> logger)
    {
        _planRepository = planRepository;
        _ruleSetRepository = ruleSetRepository;
        _logger = logger;
    }

    public async Task Handle(WeightRecordedEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing WeightRecordedEvent for AnimalId: {AnimalId}. New Weight: {Weight}", notification.AnimalId, notification.WeightKg);

        var activePlan = await _planRepository.GetActivePlanForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (activePlan != null)
        {
            var ruleSet = await _ruleSetRepository.GetByIdAsync(activePlan.FeedingRuleSetId, cancellationToken);
            if (ruleSet != null)
            {
                var matchingRule = ruleSet.Lines.FirstOrDefault(l => notification.WeightKg >= l.WeightFromKg && notification.WeightKg < l.WeightToKg);
                if (matchingRule != null)
                {
                    activePlan.UpdateCurrentRule(
                        matchingRule.Id, 
                        notification.WeightKg, 
                        matchingRule.ConcentrateKgPerDay, 
                        matchingRule.RoughageKgPerDay);
                        
                    _planRepository.Update(activePlan);
                    if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Updated feeding plan {PlanId} with new rule line {RuleLineId} based on weight {Weight}", activePlan.Id, matchingRule.Id, notification.WeightKg);
                }
            }
        }
    }
}
