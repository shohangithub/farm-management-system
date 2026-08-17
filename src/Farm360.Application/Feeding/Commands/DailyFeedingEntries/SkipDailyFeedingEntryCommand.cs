using Farm360.Application.Common.Exceptions;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public record SkipDailyFeedingEntryCommand(
    Guid EntryId,
    string Reason) : IRequest;

public class SkipDailyFeedingEntryCommandHandler : IRequestHandler<SkipDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly ILogger<SkipDailyFeedingEntryCommandHandler> _logger;

    public SkipDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        ILogger<SkipDailyFeedingEntryCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(SkipDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.DailyFeedingEntry), request.EntryId);

        entry.Skip(request.Reason);
        
        _repository.Update(entry);
        
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Skipped feeding entry {EntryId} with reason {Reason}", entry.Id, request.Reason);
    }
}
