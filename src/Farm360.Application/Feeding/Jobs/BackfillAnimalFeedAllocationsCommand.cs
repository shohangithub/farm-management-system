using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.Services;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Farm360.Application.Feeding.Jobs;

public sealed record BackfillAnimalFeedAllocationsResult(
    int EntriesProcessed,
    int AllocationsWritten,
    int EntriesWithNoAnimals,
    DateOnly? LastEntryDate);

/// <summary>
/// Creates per-animal feed allocations for feeding entries recorded before GAP-1 was closed.
/// </summary>
/// <remarks>
/// Idempotent by construction: it selects only entries that have no allocation rows, so it can be
/// re-run, resumed after a failure, or executed in chunks over several nights without
/// double-charging anyone. The unique index on (entry, animal) is the backstop.
/// </remarks>
public sealed record BackfillAnimalFeedAllocationsCommand(
    DateOnly From,
    DateOnly To,
    int BatchSize = 500,
    int MaxBatches = 200) : IRequest<BackfillAnimalFeedAllocationsResult>;

public sealed class BackfillAnimalFeedAllocationsCommandHandler
    : IRequestHandler<BackfillAnimalFeedAllocationsCommand, BackfillAnimalFeedAllocationsResult>
{
    private readonly IAnimalFeedAllocationRepository _allocationRepository;
    private readonly IAnimalFeedingPlanRepository _planRepository;
    private readonly IFeedAllocationService _allocationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BackfillAnimalFeedAllocationsCommandHandler> _logger;

    public BackfillAnimalFeedAllocationsCommandHandler(
        IAnimalFeedAllocationRepository allocationRepository,
        IAnimalFeedingPlanRepository planRepository,
        IFeedAllocationService allocationService,
        IUnitOfWork unitOfWork,
        ILogger<BackfillAnimalFeedAllocationsCommandHandler> logger)
    {
        _allocationRepository = allocationRepository;
        _planRepository = planRepository;
        _allocationService = allocationService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<BackfillAnimalFeedAllocationsResult> Handle(
        BackfillAnimalFeedAllocationsCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entriesProcessed = 0;
        var allocationsWritten = 0;
        var entriesWithNoAnimals = 0;
        DateOnly? lastEntryDate = null;

        for (var batch = 0; batch < request.MaxBatches; batch++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entries = await _allocationRepository
                .GetUnallocatedEntriesAcrossTenantsAsync(request.From, request.To, request.BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (entries.Count == 0)
            {
                break;
            }

            // One plan lookup per batch rather than per entry: a year of daily entries shares a
            // handful of plans, and this is the difference between minutes and hours.
            var plans = await _planRepository
                .GetByIdsAcrossTenantsAsync(entries.Select(e => e.FeedingPlanId).Distinct(), cancellationToken)
                .ConfigureAwait(false);

            var planById = plans.ToDictionary(p => p.Id);

            foreach (var entry in entries)
            {
                entriesProcessed++;
                lastEntryDate = entry.EntryDate;

                if (!planById.TryGetValue(entry.FeedingPlanId, out var plan))
                {
                    // Orphaned entry: the plan was hard-deleted at some point. Nothing sensible to
                    // allocate to, and skipping keeps the backfill idempotent-but-noisy rather
                    // than silently wrong.
                    entriesWithNoAnimals++;
                    continue;
                }

                var allocations = await _allocationService
                    .BuildAllocationsAsync(entry, plan, isBackfill: true, cancellationToken)
                    .ConfigureAwait(false);

                if (allocations.Count == 0)
                {
                    entriesWithNoAnimals++;
                    continue;
                }

                _allocationRepository.AddRange(allocations);
                allocationsWritten += allocations.Count;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Feed allocation backfill: batch {Batch}, {Entries} entries, {Allocations} allocations, up to {LastDate}.",
                    batch + 1, entries.Count, allocationsWritten, lastEntryDate);
            }

            // A short final batch means the range is exhausted.
            if (entries.Count < request.BatchSize)
            {
                break;
            }
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Feed allocation backfill complete: {Entries} entries, {Allocations} allocations, {Skipped} entries with no animals in scope.",
                entriesProcessed, allocationsWritten, entriesWithNoAnimals);
        }

        return new BackfillAnimalFeedAllocationsResult(
            entriesProcessed, allocationsWritten, entriesWithNoAnimals, lastEntryDate);
    }
}
