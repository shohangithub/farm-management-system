using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class VetVisitRepository(ApplicationDbContext context) : IVetVisitRepository
{
    public async Task<(IReadOnlyList<VetVisit> Items, int TotalCount)> GetPagedAsync(int pageNumber, int pageSize, Guid? farmId, CancellationToken ct = default)
    {
        var query = context.VetVisits.AsQueryable();

        if (farmId.HasValue)
        {
            query = query.Where(v => v.FarmId == farmId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(v => v.VisitDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public void Add(VetVisit visit) => context.VetVisits.Add(visit);

    public async Task<VetVisit?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.VetVisits.FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public void Update(VetVisit visit)
    {
        context.VetVisits.Update(visit);
    }
}
