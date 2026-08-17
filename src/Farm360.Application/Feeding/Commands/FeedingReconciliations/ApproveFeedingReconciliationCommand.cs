using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.FeedingReconciliations;

public record ApproveFeedingReconciliationCommand(Guid Id, string? Notes = null) : IRequest;

public class ApproveFeedingReconciliationCommandHandler : IRequestHandler<ApproveFeedingReconciliationCommand>
{
    private readonly IFeedingReconciliationRepository _repository;
    private readonly ILogger<ApproveFeedingReconciliationCommandHandler> _logger;

    public ApproveFeedingReconciliationCommandHandler(
        IFeedingReconciliationRepository repository,
        ILogger<ApproveFeedingReconciliationCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(ApproveFeedingReconciliationCommand request, CancellationToken cancellationToken)
    {
        var reconciliation = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.FeedingCycleReconciliation), request.Id);

        reconciliation.Approve(Guid.Empty); // Passing Guid.Empty as a placeholder since we don't have current user context yet.
        
        _repository.Update(reconciliation);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Approved feeding reconciliation {ReconciliationId}", reconciliation.Id);
    }
}
