using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Farm360.Application.Analytics.Queries;

public sealed record GetBreedingAnalyticsQuery(Guid? FarmId) : IRequest<BreedingAnalyticsDto>;

public sealed class GetBreedingAnalyticsQueryHandler : IRequestHandler<GetBreedingAnalyticsQuery, BreedingAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analyticsService;

    public GetBreedingAnalyticsQueryHandler(IAnalyticsQueryService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<BreedingAnalyticsDto> Handle(GetBreedingAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetBreedingAnalyticsAsync(request.FarmId, cancellationToken);
    }
}
