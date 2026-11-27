using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Farms.Repositories;
using MediatR;

namespace Farm360.Application.Analytics.Queries;

public sealed record GetFinanceAnalyticsQuery(Guid? FarmId, int Year) : IRequest<FinanceAnalyticsDto>;

public sealed class GetFinanceAnalyticsQueryHandler : IRequestHandler<GetFinanceAnalyticsQuery, FinanceAnalyticsDto>
{
    private readonly IAnalyticsQueryService _analyticsService;
    private readonly ILivestockCarriedCostService? _carried;
    private readonly IFarmRepository? _farms;
    private readonly ITenantService? _tenant;

    public GetFinanceAnalyticsQueryHandler(
        IAnalyticsQueryService analyticsService,
        ILivestockCarriedCostService? carried = null,
        IFarmRepository? farms = null,
        ITenantService? tenant = null)
    {
        _analyticsService = analyticsService;
        _carried = carried;
        _farms = farms;
        _tenant = tenant;
    }

    public async Task<FinanceAnalyticsDto> Handle(GetFinanceAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var result = await _analyticsService.GetFinanceAnalyticsAsync(request.FarmId, request.Year, cancellationToken);
        if (_carried is null)
            return result;

        // Same expenses as the statements: each month's change in the feed & care carried in the
        // animals still on the farm (farm rule) is taken off that month's costs.
        var tenantId = _tenant?.TenantId ?? Guid.Empty;
        IReadOnlyList<Guid> farmIds = request.FarmId is { } f
            ? [f]
            : _farms is null || tenantId == Guid.Empty
                ? []
                : (await _farms.GetAllByTenantAsync(tenantId, cancellationToken)).Select(x => x.Id).ToList();

        var yearEnd = new DateOnly(request.Year, 12, 31);
        var byMonth = new decimal[13];
        foreach (var farmId in farmIds)
        {
            if (!_carried.IsEnabled(farmId))
                continue;
            var movements = await _carried.GetMovementsAsync(farmId, yearEnd, cancellationToken);
            foreach (var m in movements.Where(m => m.Date.Year == request.Year))
                byMonth[m.Date.Month] += m.AmountBdt;
        }

        return result with
        {
            MonthlyData = result.MonthlyData
                .Select(m => m.Year == request.Year && m.Month is >= 1 and <= 12 && byMonth[m.Month] != 0m
                    ? m with { TotalExpenseBdt = m.TotalExpenseBdt - byMonth[m.Month] }
                    : m)
                .ToList(),
        };
    }
}
