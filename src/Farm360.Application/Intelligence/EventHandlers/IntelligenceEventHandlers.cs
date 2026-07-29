using Farm360.Application.Intelligence.Interfaces;
using Farm360.Domain.Intelligence.Interfaces.Repositories;
using Farm360.Domain.Livestock.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Intelligence.EventHandlers;

/// <summary>
/// This handler runs inside the scope created by the IntelligenceBackgroundService
/// after the event has been pulled from the asynchronous channel.
/// </summary>
public class IntelligenceEventHandlers : INotificationHandler<WeightRecordedNotification>
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IInsightRepository _insightRepository;
    private readonly ILogger<IntelligenceEventHandlers> _logger;
    private readonly Farm360.Application.Common.Interfaces.IUnitOfWork _unitOfWork;
    private readonly Farm360.Application.Common.Interfaces.INotificationService _notificationService;

    public IntelligenceEventHandlers(
        IRuleEngine ruleEngine,
        IInsightRepository insightRepository,
        ILogger<IntelligenceEventHandlers> logger,
        Farm360.Application.Common.Interfaces.IUnitOfWork unitOfWork,
        Farm360.Application.Common.Interfaces.INotificationService notificationService)
    {
        _ruleEngine = ruleEngine;
        _insightRepository = insightRepository;
        _logger = logger;
        _unitOfWork = unitOfWork;
        _notificationService = notificationService;
    }

    public async Task Handle(WeightRecordedNotification notificationWrapper, CancellationToken cancellationToken)
    {
        var notification = notificationWrapper.DomainEvent;
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Intelligence engine evaluating rules for animal {AnimalId} due to new weight record.", notification.AnimalId);
        }

        var insights = await _ruleEngine.EvaluateAnimalPerformanceAsync(notification.AnimalId, cancellationToken);
        
        if (insights.Count > 0)
        {
            foreach (var insight in insights)
            {
                await _insightRepository.AddAsync(insight, cancellationToken);
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Generated actionable insight: {Title} for Animal {AnimalId}", insight.Title, notification.AnimalId);
                }
                
                await _notificationService.SendToTenantAsync(
                    insight.TenantId,
                    "NewIntelligenceInsight",
                    new {
                        insight.Id,
                        insight.AnimalId,
                        insight.Title,
                        insight.Message,
                        Severity = insight.Severity.ToString(),
                        Type = insight.Type.ToString()
                    },
                    cancellationToken
                );
            }
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
