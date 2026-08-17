using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.FeedingReconciliations;

public record RejectFeedingReconciliationCommand(Guid Id, string Reason) : IRequest;

public class RejectFeedingReconciliationCommandHandler : IRequestHandler<RejectFeedingReconciliationCommand>
{
    private readonly IFeedingReconciliationRepository _repository;
    private readonly ILogger<RejectFeedingReconciliationCommandHandler> _logger;

    public RejectFeedingReconciliationCommandHandler(
        IFeedingReconciliationRepository repository,
        ILogger<RejectFeedingReconciliationCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(RejectFeedingReconciliationCommand request, CancellationToken cancellationToken)
    {
        var reconciliation = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.FeedingCycleReconciliation), request.Id);

        reconciliation.Reject(Guid.Empty); // Passing Guid.Empty as a placeholder since we don't have current user context yet.
        
        _repository.Update(reconciliation);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Rejected feeding reconciliation {ReconciliationId}", reconciliation.Id);
    }
}
