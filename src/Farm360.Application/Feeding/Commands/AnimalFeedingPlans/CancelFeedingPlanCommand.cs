using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.AnimalFeedingPlans;

public record CancelFeedingPlanCommand(Guid Id) : IRequest;

public class CancelFeedingPlanCommandHandler : IRequestHandler<CancelFeedingPlanCommand>
{
    private readonly IAnimalFeedingPlanRepository _repository;
    private readonly ILogger<CancelFeedingPlanCommandHandler> _logger;

    public CancelFeedingPlanCommandHandler(
        IAnimalFeedingPlanRepository repository,
        ILogger<CancelFeedingPlanCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(CancelFeedingPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.AnimalFeedingPlan), request.Id);

        plan.Cancel();
        _repository.Update(plan);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Cancelled feeding plan {PlanId}", plan.Id);
    }
}
