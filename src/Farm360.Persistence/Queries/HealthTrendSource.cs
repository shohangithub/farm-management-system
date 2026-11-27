using Farm360.Application.Analytics.Queries;
using Farm360.Domain.Health.Enums;
using Farm360.Persistence.Context;
using Farm360.Persistence.Repositories.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <summary>
/// Set-based aggregates for the Health trends page: deaths and treatments grouped by month,
/// treatments grouped by diagnosis, and the vaccination coverage counts. Every record in the
/// window is counted in SQL; nothing is paged or capped.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class HealthTrendSource(ApplicationDbContext context) : IHealthTrendSource
{
    public async Task<HealthTrendSourceData> GetAsync(
        Guid farmId, DateOnly from, DateOnly to, DateOnly vaccinatedSince, int topIllnesses,
        CancellationToken cancellationToken = default)
    {
        // Mortality and treatment rows have no farm id of their own: the farm comes from the animal.
        var farmAnimalIds = context.Animals.Where(a => a.FarmId == farmId).Select(a => a.Id);

        var deaths = await context.MortalityRecords.AsNoTracking()
            .Where(m => m.DeathDate >= from && m.DeathDate <= to && farmAnimalIds.Contains(m.AnimalId))
            .GroupBy(m => new { m.DeathDate.Year, m.DeathDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var treatmentsInWindow = context.MedicalTreatments.AsNoTracking()
            .Where(t => t.StartDate >= from && t.StartDate <= to && farmAnimalIds.Contains(t.AnimalId));

        var treatments = await treatmentsInWindow
            .GroupBy(t => new { t.StartDate.Year, t.StartDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count(), Cost = g.Sum(t => t.CostBdt) })
            .ToListAsync(cancellationToken);

        var illnesses = await treatmentsInWindow
            .Where(t => t.Diagnosis != "")
            .GroupBy(t => t.Diagnosis)
            .Select(g => new { Name = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Name)
            .Take(Math.Max(1, topIllnesses))
            .ToListAsync(cancellationToken);

        var onFarm = DashboardMetricQueries.OnFarmAnimals(context, null, farmId);
        var onFarmCount = await onFarm.CountAsync(cancellationToken);
        var vaccinated = onFarmCount == 0 ? 0 : await onFarm
            .Where(a => context.VaccinationEvents.Any(v =>
                v.AnimalId == a.Id && v.Status == VaccinationStatus.Completed && v.AdministeredDate >= vaccinatedSince))
            .CountAsync(cancellationToken);

        return new HealthTrendSourceData(
            deaths.Select(d => new HealthMonthAggregate(d.Year, d.Month, d.Count)).ToList(),
            treatments.Select(t => new HealthMonthAggregate(t.Year, t.Month, t.Count, t.Cost)).ToList(),
            illnesses.Select(i => new HealthIllnessCountDto(i.Name, i.Count)).ToList(),
            onFarmCount,
            vaccinated);
    }
}
