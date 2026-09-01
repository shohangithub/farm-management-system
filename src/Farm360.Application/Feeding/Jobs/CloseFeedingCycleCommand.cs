using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Jobs;

public sealed record CloseFeedingCycleCommand() : IRequest;

public sealed class CloseFeedingCycleCommandHandler : IRequestHandler<CloseFeedingCycleCommand>
{
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedingReconciliationRepository _reconciliationRepository;
    private readonly IDailyFeedingEntryRepository _entryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CloseFeedingCycleCommandHandler> _logger;

    public CloseFeedingCycleCommandHandler(
        IAnimalFeedingPlanRepository planRepository,
        IFeedingReconciliationRepository reconciliationRepository,
        IDailyFeedingEntryRepository entryRepository,
        IUnitOfWork unitOfWork,
        ILogger<CloseFeedingCycleCommandHandler> logger)
    {
        _planRepository = planRepository;
        _reconciliationRepository = reconciliationRepository;
        _entryRepository = entryRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(CloseFeedingCycleCommand request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Closing feeding cycles across all tenants");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Fetch plans that are active and need a reconciliation for the past cycle (e.g., past 7 or 15 days).
        // Since reconciliation involves aggregating entries, a real-world approach uses raw SQL / Dapper.
        // For demonstration within phase 3: we'll create a basic placeholder cycle.
        
        // This command will aggregate entries and create FeedingCycleReconciliation objects.
        
        await Task.CompletedTask;
    }
}
