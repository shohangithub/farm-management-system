using System.Globalization;
using Farm360.Application.Analytics.Queries;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Feeding.DTOs;
using Farm360.Domain.Feeding.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Feeding.Queries.Analytics;

public sealed record GetFcrAnalyticsQuery(Guid FarmId, Guid? ShedId = null) : IRequest<FcrAnalyticsDto>;

/// <summary>
/// FCR = Total Feed Consumed (kg) / Total Weight Gained (kg) over the last 12 months -- weight gain comes from actual
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
    /// <summary>How far back the feeding overview looks.</summary>
    private const int LookbackDays = 365;

    private readonly IFeedConsumptionLogRepository _logRepository;
    private readonly IAnimalFeedAllocationRepository _allocations;
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly ITenantService _tenantService;
    private readonly IFarmClock _clock;

    public GetFcrAnalyticsQueryHandler(
        IFeedConsumptionLogRepository logRepository,
        IAnimalFeedAllocationRepository allocations,
        IAnalyticsQueryService analyticsQueryService,
        ITenantService tenantService,
        IFarmClock clock)
    {
        _logRepository = logRepository;
        _allocations = allocations;
        _analyticsQueryService = analyticsQueryService;
        _tenantService = tenantService;
        _clock = clock;
    }

    public async Task<FcrAnalyticsDto> Handle(GetFcrAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;
        var today = _clock.TodayForFarm(tenantId, request.FarmId);
        var from = today.AddDays(-LookbackDays);

        // Feed actually eaten: the confirmed daily feeding entries (how farms record feed today),
        // plus any older shed-level logs from before daily feeding existed. Daily entries carry no
        // shed, so a shed filter only narrows the older logs.
        var days = new List<(DateOnly Date, decimal Kg, decimal CostBdt)>();

        if (!request.ShedId.HasValue)
        {
            var eaten = await _allocations
                .GetDailyEatenByFarmAsync(request.FarmId, from, today, cancellationToken)
                .ConfigureAwait(false);
            days.AddRange(eaten.Select(e => (e.Date, e.Kg, e.CostBdt)));
        }

        var logs = await _logRepository.GetLogsAsync(tenantId, request.FarmId, null, null, cancellationToken).ConfigureAwait(false);
        days.AddRange(logs
            .Where(l => (!request.ShedId.HasValue || l.ShedId == request.ShedId.Value) && l.LogDate >= from && l.LogDate <= today)
            .Select(l => (l.LogDate, l.NetConsumptionKg, l.TotalCostBdt)));

        decimal totalFeedConsumedKg = days.Sum(d => d.Kg);
        decimal totalFeedCostBdt = days.Sum(d => d.CostBdt);

        decimal totalWeightGainKg = 0m;
        if (days.Count > 0)
        {
            totalWeightGainKg = await _analyticsQueryService
                .GetTotalWeightGainKgAsync(request.FarmId, days.Min(d => d.Date), days.Max(d => d.Date), cancellationToken)
                .ConfigureAwait(false);
        }

        // No real gain to divide by (no weight records in the period, or the herd lost weight
        // overall) -- FCR is undefined here. Report 0 rather than fabricate a number.
        decimal fcrValue = totalWeightGainKg > 0m ? Math.Round(totalFeedConsumedKg / totalWeightGainKg, 2) : 0m;
        decimal costPerKgGain = totalWeightGainKg > 0m ? Math.Round(totalFeedCostBdt / totalWeightGainKg, 2) : 0m;

        var monthlyTrends = new List<MonthlyFcrDataPointDto>();
        foreach (var group in days
                     .GroupBy(d => new { d.Date.Year, d.Date.Month })
                     .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                     .TakeLast(12))
        {
            string monthLabel = new DateTime(group.Key.Year, group.Key.Month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);
            decimal mFeed = group.Sum(x => x.Kg);
            decimal mGain = await _analyticsQueryService
                .GetTotalWeightGainKgAsync(request.FarmId, group.Min(x => x.Date), group.Max(x => x.Date), cancellationToken)
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
