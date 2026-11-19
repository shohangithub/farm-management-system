using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Domain.Livestock;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class HealthDashboardRepository(ApplicationDbContext context,
    IFarmClock? clock = null) : IHealthDashboardRepository
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    // Due/overdue/active work only exists for animals still on the farm (Active + Quarantined —
    // AnimalStatusGroups.OnFarm). A sold or dead animal's open schedule is not work anyone can do.
    private IQueryable<Animal> OnFarmAnimals(Guid tenantId, Guid? farmId) =>
        context.Animals.AsNoTracking().Where(a =>
            a.TenantId == tenantId &&
            AnimalStatusGroups.OnFarm.Contains(a.Status) &&
            (farmId == null || a.FarmId == farmId));

    // All animals of the farm (history: costs, mortality) regardless of status.
    private IQueryable<Animal> FarmAnimals(Guid tenantId, Guid? farmId) =>
        context.Animals.AsNoTracking().Where(a => a.TenantId == tenantId && (farmId == null || a.FarmId == farmId));

    public async Task<int> GetVaccinationsDueThisWeekAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        var today = _clock.Today;
        var endOfWeek = today.AddDays(7);

        return await context.VaccinationEvents
            .Where(v => v.TenantId == tenantId &&
                        v.Status == VaccinationStatus.Scheduled &&
                        v.ScheduledDate >= today && v.ScheduledDate <= endOfWeek)
            .Join(OnFarmAnimals(tenantId, farmId), v => v.AnimalId, a => a.Id, (v, a) => v.Id)
            .CountAsync(ct);
    }

    public async Task<int> GetVaccinationsOverdueAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        var today = _clock.Today;

        return await context.VaccinationEvents
            .Where(v => v.TenantId == tenantId &&
                        (v.Status == VaccinationStatus.Scheduled || v.Status == VaccinationStatus.Overdue) &&
                        v.ScheduledDate < today)
            .Join(OnFarmAnimals(tenantId, farmId), v => v.AnimalId, a => a.Id, (v, a) => v.Id)
            .CountAsync(ct);
    }

    public async Task<int> GetActiveTreatmentsAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        return await context.MedicalTreatments
            .Where(t => t.TenantId == tenantId && t.Status == TreatmentStatus.Ongoing)
            .Join(OnFarmAnimals(tenantId, farmId), t => t.AnimalId, a => a.Id, (t, a) => t.Id)
            .CountAsync(ct);
    }

    public async Task<int> GetActiveIncidentsAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        return await context.DiseaseIncidents
            .CountAsync(i => i.TenantId == tenantId && 
                             (farmId == null || i.FarmId == farmId) &&
                             i.Status != IncidentStatus.Resolved, ct);
    }

    public async Task<int> GetRecentMortalityCountAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        var thirtyDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        return await context.MortalityRecords
            .Where(m => m.TenantId == tenantId && m.DeathDate >= thirtyDaysAgo)
            .Join(FarmAnimals(tenantId, farmId), m => m.AnimalId, a => a.Id, (m, a) => m.Id)
            .CountAsync(ct);
    }

    public async Task<decimal> GetMonthlyHealthCostAsync(Guid tenantId, Guid? farmId = null, CancellationToken ct = default)
    {
        var thirtyDaysAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        // Cost is history: treatments of animals since sold/dead still count.
        var treatmentCost = await context.MedicalTreatments
            .Where(t => t.TenantId == tenantId && t.StartDate >= thirtyDaysAgo)
            .Join(FarmAnimals(tenantId, farmId), t => t.AnimalId, a => a.Id, (t, a) => t.CostBdt)
            .SumAsync(c => (decimal?)c, ct) ?? 0;
            
        var vetCost = await context.VetVisits
            .Where(v => v.TenantId == tenantId && (farmId == null || v.FarmId == farmId) && v.VisitDate >= thirtyDaysAgo)
            .SumAsync(v => (decimal?)v.CostBdt, ct) ?? 0;

        return treatmentCost + vetCost;
    }
}
