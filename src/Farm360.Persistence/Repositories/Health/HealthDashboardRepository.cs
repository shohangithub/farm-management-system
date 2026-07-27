using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class HealthDashboardRepository(ApplicationDbContext context) : IHealthDashboardRepository
{
    public async Task<int> GetVaccinationsDueThisWeekAsync(Guid tenantId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var endOfWeek = today.AddDays(7);

        return await context.VaccinationEvents
            .CountAsync(v => v.TenantId == tenantId && 
                             v.Status == VaccinationStatus.Scheduled &&
                             v.ScheduledDate >= today && v.ScheduledDate <= endOfWeek, ct);
    }

    public async Task<int> GetVaccinationsOverdueAsync(Guid tenantId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await context.VaccinationEvents
            .CountAsync(v => v.TenantId == tenantId && 
                             v.Status == VaccinationStatus.Scheduled &&
                             v.ScheduledDate < today, ct);
    }

    public async Task<int> GetActiveTreatmentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await context.MedicalTreatments
            .CountAsync(t => t.TenantId == tenantId && 
                             t.Status == TreatmentStatus.Ongoing, ct);
    }

    public async Task<int> GetActiveIncidentsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await context.DiseaseIncidents
            .CountAsync(i => i.TenantId == tenantId && 
                             i.Status != IncidentStatus.Resolved, ct);
    }

    public async Task<int> GetRecentMortalityCountAsync(Guid tenantId, CancellationToken ct = default)
    {
        var thirtyDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        return await context.MortalityRecords
            .CountAsync(m => m.TenantId == tenantId && 
                             m.DeathDate >= thirtyDaysAgo, ct);
    }

    public async Task<decimal> GetMonthlyHealthCostAsync(Guid tenantId, CancellationToken ct = default)
    {
        var thirtyDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        var treatmentCost = await context.MedicalTreatments
            .Where(t => t.TenantId == tenantId && t.StartDate >= thirtyDaysAgo)
            .SumAsync(t => t.CostBdt, ct);
            
        var vetCost = await context.VetVisits
            .Where(v => v.TenantId == tenantId && v.VisitDate >= thirtyDaysAgo)
            .SumAsync(v => v.CostBdt ?? 0, ct);

        return treatmentCost + vetCost;
    }
}
