using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class AnimalSoldEventHandler : INotificationHandler<AnimalSoldEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly ILogger<AnimalSoldEventHandler> _logger;

    public AnimalSoldEventHandler(IAnimalFeedingPlanRepository planRepository, ILogger<AnimalSoldEventHandler> logger)
    {
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task Handle(AnimalSoldEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing AnimalSoldEvent for AnimalId: {AnimalId}. Cancelling any active feeding plans.", notification.AnimalId);

        var activePlan = await _planRepository.GetActivePlanForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (activePlan != null)
        {
            activePlan.Cancel();
            _planRepository.Update(activePlan);
            
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Cancelled active feeding plan {PlanId} for sold animal {AnimalId}", activePlan.Id, notification.AnimalId);
        }
    }
}
