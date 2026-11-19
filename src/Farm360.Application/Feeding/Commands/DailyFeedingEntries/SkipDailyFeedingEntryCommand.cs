using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using Farm360.Domain.Finance.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Commands.DailyFeedingEntries;

public record SkipDailyFeedingEntryCommand(
    Guid EntryId,
    string Reason) : IRequest;

/// <summary>
/// Skips a pending entry. The per-animal rows written when the entry was generated carried the
/// planned feed and cost; a skipped day fed nothing, so they are zeroed and every animal they
/// touched has its ledger feed bucket re-set from its confirmed total.
/// </summary>
public class SkipDailyFeedingEntryCommandHandler : IRequestHandler<SkipDailyFeedingEntryCommand>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly ILogger<SkipDailyFeedingEntryCommandHandler> _logger;
    private readonly IAnimalFeedAllocationRepository? _allocationRepository;
    private readonly IAnimalCostLedgerRepository? _ledgerRepository;
    private readonly IUnitOfWork? _unitOfWork;

    public SkipDailyFeedingEntryCommandHandler(
        IDailyFeedingEntryRepository repository,
        ILogger<SkipDailyFeedingEntryCommandHandler> logger,
        IAnimalFeedAllocationRepository? allocationRepository = null,
        IAnimalCostLedgerRepository? ledgerRepository = null,
        IUnitOfWork? unitOfWork = null)
    {
        _repository = repository;
        _logger = logger;
        _allocationRepository = allocationRepository;
        _ledgerRepository = ledgerRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(SkipDailyFeedingEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await _repository.GetByIdAsync(request.EntryId, cancellationToken)
            ?? throw new NotFoundException(nameof(Farm360.Domain.Feeding.DailyFeedingEntry), request.EntryId);

        var allocations = await FeedingEntrySkip.SkipAsync(entry, request.Reason, _repository, _allocationRepository, cancellationToken);

        if (_unitOfWork is not null)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (_allocationRepository is not null && _ledgerRepository is not null && allocations.Count > 0)
            {
                await AnimalFeedCostSync.SyncAsync(_allocationRepository, _ledgerRepository, entry.TenantId, entry.FarmId,
                    allocations.Select(a => a.AnimalId), cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Skipped feeding entry {EntryId} with reason {Reason}", entry.Id, request.Reason);
    }
}
