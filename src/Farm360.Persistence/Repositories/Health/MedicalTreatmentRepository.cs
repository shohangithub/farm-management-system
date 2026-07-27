using Farm360.Domain.Health;
using Farm360.Domain.Health.Enums;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class MedicalTreatmentRepository(ApplicationDbContext context) : IMedicalTreatmentRepository
{
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

    public async Task<(IReadOnlyList<MedicalTreatment> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? animalId, CancellationToken ct = default)
    {
        var query = context.MedicalTreatments.AsQueryable();

        if (animalId.HasValue)
        {
            query = query.Where(mt => mt.AnimalId == animalId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(mt => mt.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<(MedicalTreatment Treatment, string AnimalTag)>> GetActiveMilkWithdrawalsAsync(Guid farmId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = from mt in context.MedicalTreatments
                    join a in context.Animals on mt.AnimalId equals a.Id
                    where a.FarmId == farmId && mt.WithdrawalPeriod.MilkDays > 0
                    let safeDate = mt.StartDate.AddDays(mt.WithdrawalPeriod.MilkDays)
                    where safeDate > today
                    orderby safeDate
                    select new { Treatment = mt, TagNumber = a.Tag.TagId };

        var results = await query.ToListAsync(ct);

        return results.Select(x => (x.Treatment, x.TagNumber)).ToList();
    }

    public void Add(MedicalTreatment treatment) => context.MedicalTreatments.Add(treatment);
    public void Update(MedicalTreatment treatment) => context.MedicalTreatments.Update(treatment);
}
