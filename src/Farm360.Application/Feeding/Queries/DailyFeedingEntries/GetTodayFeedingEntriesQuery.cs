using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Feeding.Enums;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.DailyFeedingEntries;

public record DailyFeedingEntryDto(
    Guid Id,
    Guid FeedingPlanId,
    Guid? ShedId,
    Guid? PenId,
    Guid? BatchId,
    Guid FormulaId,
    decimal ExpectedKg,
    decimal? ActualKg,
    DailyFeedingEntryStatus Status);

public sealed record GetTodayFeedingEntriesQuery(Guid FarmId) : IRequest<IReadOnlyList<DailyFeedingEntryDto>>;

public sealed class GetTodayFeedingEntriesQueryHandler : IRequestHandler<GetTodayFeedingEntriesQuery, IReadOnlyList<DailyFeedingEntryDto>>
{
    private readonly IDailyFeedingEntryRepository _repository;
    private readonly ITenantService _tenantService;

    public GetTodayFeedingEntriesQueryHandler(IDailyFeedingEntryRepository repository, ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<IReadOnlyList<DailyFeedingEntryDto>> Handle(GetTodayFeedingEntriesQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entries = await _repository.GetEntriesByDateAsync(_tenantService.TenantId, request.FarmId, today, cancellationToken);

        return entries.Select(e => new DailyFeedingEntryDto(
            e.Id,
            e.FeedingPlanId,
            e.ShedId,
            e.PenId,
            e.BatchId,
            e.FormulaId,
            e.ExpectedKg,
            e.ActualKg,
            e.Status)).ToList();
    }
}
