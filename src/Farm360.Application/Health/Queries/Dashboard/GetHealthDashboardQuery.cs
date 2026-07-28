using Farm360.Application.Common.Interfaces;
using Farm360.Application.Health.DTOs;
using Farm360.Domain.Health.Interfaces.Repositories;
using MediatR;

namespace Farm360.Application.Health.Queries.Dashboard;

public sealed record GetHealthDashboardQuery(Guid? FarmId = null) : IRequest<HealthDashboardDto>;

internal sealed class GetHealthDashboardQueryHandler : IRequestHandler<GetHealthDashboardQuery, HealthDashboardDto>
{
    private readonly IHealthDashboardRepository _repository;
    private readonly ITenantService _tenantService;

    public GetHealthDashboardQueryHandler(
        IHealthDashboardRepository repository,
        ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<HealthDashboardDto> Handle(GetHealthDashboardQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantService.TenantId;

        var vaccinationsDueThisWeek = await _repository.GetVaccinationsDueThisWeekAsync(tenantId, request.FarmId, cancellationToken);
        var vaccinationsOverdue = await _repository.GetVaccinationsOverdueAsync(tenantId, request.FarmId, cancellationToken);
        var activeTreatments = await _repository.GetActiveTreatmentsAsync(tenantId, request.FarmId, cancellationToken);
        var activeIncidents = await _repository.GetActiveIncidentsAsync(tenantId, request.FarmId, cancellationToken);
        var recentMortalityCount = await _repository.GetRecentMortalityCountAsync(tenantId, request.FarmId, cancellationToken);
        var monthlyHealthCost = await _repository.GetMonthlyHealthCostAsync(tenantId, request.FarmId, cancellationToken);

        return new HealthDashboardDto(
            vaccinationsDueThisWeek,
            vaccinationsOverdue,
            activeTreatments,
            activeIncidents,
            recentMortalityCount,
            monthlyHealthCost
        );
    }
}
