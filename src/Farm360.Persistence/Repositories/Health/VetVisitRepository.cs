using Farm360.Domain.Health;
using Farm360.Domain.Health.Interfaces.Repositories;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Repositories.Health;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class VetVisitRepository(ApplicationDbContext context) : IVetVisitRepository
{
    public async Task<(IReadOnlyList<VetVisit> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken ct = default)
    {
        var query = context.VetVisits.AsNoTracking().AsQueryable();

        if (farmId.HasValue)
        {
            query = query.Where(v => v.FarmId == farmId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(v => EF.Functions.Like(v.VetName, $"%{term}%") ||
                                     (v.Purpose != null && EF.Functions.Like(v.Purpose, $"%{term}%")) ||
                                     (v.Findings != null && EF.Functions.Like(v.Findings, $"%{term}%")));
        }

        var totalCount = await query.CountAsync(ct);

        query = (sortBy?.ToLowerInvariant(), sortDescending) switch
        {
            ("visitdate", false)    => query.OrderBy(v => v.VisitDate),
            ("visitdate", true)     => query.OrderByDescending(v => v.VisitDate),
            ("vetname", false)      => query.OrderBy(v => v.VetName),
            ("vetname", true)       => query.OrderByDescending(v => v.VetName),
            ("costbdt", false)      => query.OrderBy(v => v.CostBdt),
            ("costbdt", true)       => query.OrderByDescending(v => v.CostBdt),
            ("createdat", false)    => query.OrderBy(v => v.CreatedAt),
            _                       => query.OrderByDescending(v => v.VisitDate)
        };

        var items = await query
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
