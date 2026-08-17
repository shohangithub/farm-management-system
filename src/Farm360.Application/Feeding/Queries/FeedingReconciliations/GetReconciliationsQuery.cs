using MediatR;
using Farm360.Domain.Feeding.Enums;

namespace Farm360.Application.Feeding.Queries.FeedingReconciliations;

public record GetReconciliationsQuery(Guid FarmId, string? Status) : IRequest<List<FeedingReconciliationDto>>;

public record FeedingReconciliationDto(
    Guid Id,
    DateOnly CycleDate,
    string Status,
    string? Notes);

public class GetReconciliationsQueryHandler : IRequestHandler<GetReconciliationsQuery, List<FeedingReconciliationDto>>
{
    // Mock implementation for Phase 4 to satisfy compilation and basic endpoint testing
    // Full implementation requires DbContext or Dapper querying over FeedingCycleReconciliation table.
    
    public Task<List<FeedingReconciliationDto>> Handle(GetReconciliationsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new List<FeedingReconciliationDto>());
    }
}
