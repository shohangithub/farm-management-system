using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;

namespace Farm360.Application.Analytics.Queries;

public sealed record GetFinanceAnalyticsQuery(Guid? FarmId, int Year) : IRequest<FinanceAnalyticsDto>;

public sealed class GetFinanceAnalyticsQueryHandler : IRequestHandler<GetFinanceAnalyticsQuery, FinanceAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analyticsService;

    public GetFinanceAnalyticsQueryHandler(IAnalyticsQueryService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    public async Task<FinanceAnalyticsDto> Handle(GetFinanceAnalyticsQuery request, CancellationToken cancellationToken)
    {
        return await _analyticsService.GetFinanceAnalyticsAsync(request.FarmId, request.Year, cancellationToken);
    }
}
