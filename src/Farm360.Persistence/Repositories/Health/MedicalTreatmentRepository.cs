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

    public void Add(MedicalTreatment treatment) => context.MedicalTreatments.Add(treatment);
    public void Update(MedicalTreatment treatment) => context.MedicalTreatments.Update(treatment);
}
