using System.Globalization;
using Farm360.Application.Analytics.Queries;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.DTOs;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Analytics;

public sealed record GetFcrAnalyticsQuery(Guid FarmId, Guid? ShedId = null) : IRequest<FcrAnalyticsDto>;

/// <summary>
/// FCR = Total Feed Consumed (kg) / Total Weight Gained (kg) -- weight gain comes from actual
/// WeightRecord entries (<see cref="IAnalyticsQueryService.GetTotalWeightGainKgAsync"/>), not a
/// guess. A prior version estimated weight gain as feed/6.5, which made every FCR value exactly
/// 6.5 regardless of the herd's real performance -- a fabricated number that happened to look
/// plausible. This still doesn't account for a ShedId filter (weight gain is computed farm-wide,
/// since an animal's shed membership at a point in time isn't tracked on WeightRecord itself) --
/// shed-filtered FCR will overstate weight gain slightly if the farm has multiple sheds; flagged
/// rather than silently approximated further.
/// </summary>
public sealed class GetFcrAnalyticsQueryHandler : IRequestHandler<GetFcrAnalyticsQuery, FcrAnalyticsDto>
{
    private readonly IFeedConsumptionLogRepository _logRepository;
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly ITenantService _tenantService;

    public GetFcrAnalyticsQueryHandler(
        IFeedConsumptionLogRepository logRepository,
        IAnalyticsQueryService analyticsQueryService,
        ITenantService tenantService)
    {
        _logRepository = logRepository;
        _analyticsQueryService = analyticsQueryService;
        _tenantService = tenantService;
    }

    public async Task<FcrAnalyticsDto> Handle(GetFcrAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var logs = await _logRepository.GetLogsAsync(_tenantService.TenantId, request.FarmId, null, null, cancellationToken);
        if (request.ShedId.HasValue)
        {
            logs = logs.Where(l => l.ShedId == request.ShedId.Value).ToList();
        }

        decimal totalFeedConsumedKg = logs.Sum(l => l.NetConsumptionKg);
        decimal totalFeedCostBdt = logs.Sum(l => l.TotalCostBdt);

        decimal totalWeightGainKg = 0m;
        if (logs.Count > 0)
        {
            var periodStart = logs.Min(l => l.LogDate);
            var periodEnd = logs.Max(l => l.LogDate);
            totalWeightGainKg = await _analyticsQueryService
                .GetTotalWeightGainKgAsync(request.FarmId, periodStart, periodEnd, cancellationToken)
                .ConfigureAwait(false);
        }

        // No real gain to divide by (no weight records in the period, or the herd lost weight
        // overall) -- FCR is undefined here, not "assume a textbook ratio". Report 0 rather than
        // fabricate a number.
        decimal fcrValue = totalWeightGainKg > 0m ? Math.Round(totalFeedConsumedKg / totalWeightGainKg, 2) : 0m;
        decimal costPerKgGain = totalWeightGainKg > 0m ? Math.Round(totalFeedCostBdt / totalWeightGainKg, 2) : 0m;

        var monthlyGroups = logs
            .GroupBy(l => new { l.LogDate.Year, l.LogDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Take(12);

        var monthlyTrends = new List<MonthlyFcrDataPointDto>();
        foreach (var group in monthlyGroups)
        {
            string monthLabel = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);
            decimal mFeed = group.Sum(x => x.NetConsumptionKg);
            var mStart = group.Min(x => x.LogDate);
            var mEnd = group.Max(x => x.LogDate);
            decimal mGain = await _analyticsQueryService
                .GetTotalWeightGainKgAsync(request.FarmId, mStart, mEnd, cancellationToken)
                .ConfigureAwait(false);
            decimal mFcr = mGain > 0m ? Math.Round(mFeed / mGain, 2) : 0m;
            monthlyTrends.Add(new MonthlyFcrDataPointDto(monthLabel, mFeed, mGain, mFcr));
        }

        return new FcrAnalyticsDto(
            request.FarmId,
            request.ShedId,
            null,
            totalFeedConsumedKg,
            Math.Round(totalWeightGainKg, 2),
            fcrValue,
            totalFeedCostBdt,
            costPerKgGain,
            monthlyTrends);
    }
}
