using Farm360.Application.Common.Interfaces;
using Farm360.Application.Dashboard.DTOs;
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

    public GetExecutiveDashboardQueryHandler(
        IExecutiveDashboardRepository repository,
        ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<ExecutiveDashboardDto> Handle(GetExecutiveDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var totalAnimalsTask = _repository.GetTotalAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var sickAnimalsTask = _repository.GetSickAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var lowStockTask = _repository.GetFeedLowStockCountAsync(tenantId, request.FarmId, cancellationToken);
        var incomeTask = _repository.GetCurrentMonthIncomeAsync(tenantId, request.FarmId, cancellationToken);
        var expenseTask = _repository.GetCurrentMonthExpenseAsync(tenantId, request.FarmId, cancellationToken);
        var insightsTask = _repository.GetActiveInsightsAsync(tenantId, request.FarmId, cancellationToken);

        await Task.WhenAll(
            totalAnimalsTask, 
            sickAnimalsTask, 
            lowStockTask, 
            incomeTask, 
            expenseTask, 
            insightsTask);

        var insights = (await insightsTask).Select(i => new ActionableInsightDto(
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
            await totalAnimalsTask,
            await sickAnimalsTask,
            await lowStockTask,
            await incomeTask,
            await expenseTask,
            insights
        );
    }
}
