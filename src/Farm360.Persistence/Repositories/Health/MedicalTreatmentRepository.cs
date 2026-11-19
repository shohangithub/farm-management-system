using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class MedicalTreatmentRepository(ApplicationDbContext context,
    IFarmClock? clock = null) : IMedicalTreatmentRepository
{
    private readonly IFarmClock _clock = clock ?? Farm360.Application.Common.Time.FarmClock.System;

    public async Task<MedicalTreatment?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.MedicalTreatments
            .FirstOrDefaultAsync(mt => mt.Id == id, ct);
    }

    public async Task<IReadOnlyList<MedicalTreatment>> GetByAnimalIdAsync(Guid animalId, CancellationToken ct = default)
    {
        return await context.MedicalTreatments
            .Where(mt => mt.AnimalId == animalId)
            .OrderByDescending(mt => mt.StartDate)
            .ToListAsync(ct);
    }

    public async Task<bool> HasActiveTreatmentForMedicationAsync(Guid animalId, string medicationName, CancellationToken ct = default)
    {
        return await context.MedicalTreatments
            .AnyAsync(mt => mt.AnimalId == animalId && 
                            mt.MedicationName == medicationName && 
                            mt.Status == TreatmentStatus.Ongoing, ct);
    }

    public async Task<(IReadOnlyList<MedicalTreatment> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? animalId = null,
        TreatmentStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = context.MedicalTreatments.AsNoTracking().AsQueryable();

        if (farmId.HasValue)
        {
            var animalIdsInFarm = context.Animals.Where(a => a.FarmId == farmId.Value).Select(a => a.Id);
            query = query.Where(mt => animalIdsInFarm.Contains(mt.AnimalId));
        }

        if (animalId.HasValue)
        {
            query = query.Where(mt => mt.AnimalId == animalId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(mt => mt.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(mt => EF.Functions.Like(mt.Diagnosis, $"%{term}%") ||
                                      EF.Functions.Like(mt.MedicationName, $"%{term}%") ||
                                      (mt.VeterinarianName != null && EF.Functions.Like(mt.VeterinarianName, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("startdate", false)       => query.OrderBy(mt => mt.StartDate),
            ("startdate", true)        => query.OrderByDescending(mt => mt.StartDate),
            ("medicationname", false)  => query.OrderBy(mt => mt.MedicationName),
            ("medicationname", true)   => query.OrderByDescending(mt => mt.MedicationName),
            ("costbdt", false)         => query.OrderBy(mt => mt.CostBdt),
            ("costbdt", true)          => query.OrderByDescending(mt => mt.CostBdt),
            ("createdat", false)       => query.OrderBy(mt => mt.CreatedAtUtc),
            _                          => query.OrderByDescending(mt => mt.StartDate)
        };

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<(MedicalTreatment Treatment, string AnimalTag)>> GetActiveMilkWithdrawalsAsync(Guid farmId, CancellationToken ct = default)
    {
        var today = _clock.Today;

        var query = from mt in context.MedicalTreatments
                    join a in context.Animals on mt.AnimalId equals a.Id
                    // A sold/dead animal produces no milk on this farm: withdrawal alerts are for on-farm animals only.
                    where a.FarmId == farmId && mt.WithdrawalPeriod.MilkDays > 0
                          && Farm360.Domain.Livestock.AnimalStatusGroups.OnFarm.Contains(a.Status)
                    let safeDate = mt.StartDate.AddDays(mt.WithdrawalPeriod.MilkDays)
                    where safeDate > today
                    orderby safeDate
                    select new { Treatment = mt, TagNumber = a.Tag.TagId };

        var results = await query.ToListAsync(ct);

        return results.Select(x => (x.Treatment, x.TagNumber)).ToList();
    }

    public async Task<IReadOnlyList<MedicalTreatment>> GetByFarmStartedBetweenAsync(Guid farmId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var animalIdsInFarm = context.Animals.Where(a => a.FarmId == farmId).Select(a => a.Id);
        return await context.MedicalTreatments
            .AsNoTracking()
            .Where(mt => animalIdsInFarm.Contains(mt.AnimalId) && mt.StartDate >= from && mt.StartDate <= to)
            .OrderBy(mt => mt.StartDate)
            .ToListAsync(ct);
    }

    public void Add(MedicalTreatment treatment) => context.MedicalTreatments.Add(treatment);
    public void Update(MedicalTreatment treatment) => context.MedicalTreatments.Update(treatment);
}
