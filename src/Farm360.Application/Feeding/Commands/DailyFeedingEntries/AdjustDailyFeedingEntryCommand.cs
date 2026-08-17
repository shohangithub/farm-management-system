using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public record AdjustDailyFeedingEntryCommand(
    Guid EntryId,
    decimal ActualKg,
    string? Notes) : IRequest;

public class AdjustDailyFeedingEntryCommandHandler : IRequestHandler<AdjustDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly ILogger<AdjustDailyFeedingEntryCommandHandler> _logger;

    public AdjustDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        ILogger<AdjustDailyFeedingEntryCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(AdjustDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.DailyFeedingEntry), request.EntryId);

        entry.Confirm(request.ActualKg, null, request.Notes);
        
        _repository.Update(entry);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Adjusted feeding entry {EntryId} with actual {ActualKg} kg", entry.Id, request.ActualKg);
    }
}
