using Farm360.Application.Common.Interfaces;
using Farm360.Application.Dashboard.DTOs;
using Farm360.Application.Finance.Services;
using Farm360.Domain.Farms.Repositories;
using Farm360.Domain.Dashboard.Interfaces;
using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Dashboard.Queries;

public sealed record GetExecutiveDashboardQuery(Guid? FarmId = null) : IRequest<ExecutiveDashboardDto>;

internal sealed class GetExecutiveDashboardQueryHandler : IRequestHandler<GetExecutiveDashboardQuery, ExecutiveDashboardDto>
{
    private readonly IExecutiveDashboardRepository _repository;
    private readonly ITenantService _tenantService;
    private readonly ILivestockCarriedCostService? _carried;
    private readonly IFarmRepository? _farms;

    public GetExecutiveDashboardQueryHandler(
        IExecutiveDashboardRepository repository,
        ITenantService tenantService,
        ILivestockCarriedCostService? carried = null,
        IFarmRepository? farms = null)
    {
        _repository = repository;
        _tenantService = tenantService;
        _carried = carried;
        _farms = farms;
    }

    public async Task<ExecutiveDashboardDto> Handle(GetExecutiveDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var totalAnimals = await _repository.GetTotalAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var sickAnimals = await _repository.GetSickAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var lowStock = await _repository.GetFeedLowStockCountAsync(tenantId, request.FarmId, cancellationToken);
        var income = await _repository.GetCurrentMonthIncomeAsync(tenantId, request.FarmId, cancellationToken);
        var expense = await _repository.GetCurrentMonthExpenseAsync(tenantId, request.FarmId, cancellationToken);
        // Same profit as the statements: feed & care of animals still on the farm is carried forward.
        expense -= await CarriedAdjustmentAsync(tenantId, request.FarmId, cancellationToken);
        var births = await _repository.GetBirthsThisMonthAsync(tenantId, request.FarmId, cancellationToken);
        var deaths = await _repository.GetDeathsThisMonthAsync(tenantId, request.FarmId, cancellationToken);
        var dueVaccinations = await _repository.GetDueVaccinationsAsync(tenantId, request.FarmId, cancellationToken);
        var pregnantAnimals = await _repository.GetPregnantAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var insightsList = await _repository.GetActiveInsightsAsync(tenantId, request.FarmId, cancellationToken);

        var insights = insightsList.Select(i => new ActionableInsightDto(
            i.Id,
            i.FarmId,
            i.AnimalId,
            i.BatchId,
            i.Type,
            i.Severity,
            i.Title,
            i.Message,
            i.ActionData,
            i.IsRead,
            i.CreatedAtUtc
        )).ToList();

        return new ExecutiveDashboardDto(
            totalAnimals,
            sickAnimals,
            lowStock,
            income,
            expense,
            births,
            deaths,
            dueVaccinations,
            pregnantAnimals,
            insights
        );
    }

    /// <summary>This month's change in the feed &amp; care carried in the animals on the farm (0 when the farm rule is off).</summary>
    private async Task<decimal> CarriedAdjustmentAsync(Guid tenantId, Guid? farmId, CancellationToken cancellationToken)
    {
        if (_carried is null)
            return 0m;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var farmIds = farmId is { } f
            ? [f]
            : _farms is null || tenantId == Guid.Empty
                ? []
                : (await _farms.GetAllByTenantAsync(tenantId, cancellationToken)).Select(x => x.Id).ToList();

        var total = 0m;
        foreach (var id in farmIds)
            total += await _carried.GetPeriodAdjustmentAsync(id, monthStart, today, cancellationToken);
        return total;
    }
}
