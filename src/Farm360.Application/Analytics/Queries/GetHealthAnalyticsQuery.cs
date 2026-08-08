using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Farm360.Application.Analytics.Queries;

public sealed record GetHealthAnalyticsQuery(Guid? FarmId) : IRequest<HealthAnalyticsDto>;

public sealed class GetHealthAnalyticsQueryHandler : IRequestHandler<GetHealthAnalyticsQuery, HealthAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analyticsService;

    public GetHealthAnalyticsQueryHandler(IAnalyticsQueryService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<HealthAnalyticsDto> Handle(GetHealthAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetHealthAnalyticsAsync(request.FarmId, cancellationToken);
    }
}
