using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.EventHandlers;

public sealed class AnimalDiedEventHandler : INotificationHandler<AnimalDiedEvent>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly ILogger<AnimalDiedEventHandler> _logger;

    public AnimalDiedEventHandler(IAnimalFeedingPlanRepository planRepository, ILogger<AnimalDiedEventHandler> logger)
    {
        _planRepository = planRepository;
        _logger = logger;
    }

    public async Task Handle(AnimalDiedEvent notification, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Processing AnimalDiedEvent for AnimalId: {AnimalId}. Cancelling any active feeding plans.", notification.AnimalId);

        var activePlan = await _planRepository.GetActivePlanForAnimalAsync(notification.TenantId, notification.AnimalId, cancellationToken);
        if (activePlan != null)
        {
            activePlan.Cancel();
            _planRepository.Update(activePlan);
            
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Cancelled active feeding plan {PlanId} for dead animal {AnimalId}", activePlan.Id, notification.AnimalId);
        }
    }
}
