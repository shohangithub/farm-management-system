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

        var totalAnimals = await _repository.GetTotalAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var sickAnimals = await _repository.GetSickAnimalsAsync(tenantId, request.FarmId, cancellationToken);
        var lowStock = await _repository.GetFeedLowStockCountAsync(tenantId, request.FarmId, cancellationToken);
        var income = await _repository.GetCurrentMonthIncomeAsync(tenantId, request.FarmId, cancellationToken);
        var expense = await _repository.GetCurrentMonthExpenseAsync(tenantId, request.FarmId, cancellationToken);
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
}
