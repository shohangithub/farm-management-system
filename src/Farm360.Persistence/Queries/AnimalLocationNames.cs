using Farm360.Application.Livestock.Queries;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <summary>
/// Shed and pen labels for a page of animals: one <c>WHERE Id IN (...)</c> query per table.
/// Tenant and soft-delete filters apply as usual.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class AnimalLocationNames(ApplicationDbContext context) : IAnimalLocationNames
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetShedNamesAsync(IReadOnlyCollection<Guid> shedIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shedIds);
        if (shedIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = shedIds.Distinct().ToList();
        var rows = await context.Sheds.AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.ShedName, s.ShedNumber })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => string.IsNullOrWhiteSpace(r.ShedName) ? r.ShedNumber : r.ShedName);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetPenNamesAsync(IReadOnlyCollection<Guid> penIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(penIds);
        if (penIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = penIds.Distinct().ToList();
        var rows = await context.Pens.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.PenNumber, p.PenName })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => string.IsNullOrWhiteSpace(r.PenNumber) ? r.PenName : r.PenNumber);
    }
}
