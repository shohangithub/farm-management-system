using Farm360.Application.Livestock.Queries;
using Farm360.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Farm360.Persistence.Queries;

/// <summary>Animal tag numbers for a page of health rows: one <c>WHERE Id IN (...)</c> projection.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by DI")]
internal sealed class AnimalTagNames(ApplicationDbContext context) : IAnimalTagNames
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetTagsAsync(IReadOnlyCollection<Guid> animalIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animalIds);
        if (animalIds.Count == 0)
            return new Dictionary<Guid, string>();

        var ids = animalIds.Distinct().ToList();
        var rows = await context.Animals.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.Tag.TagId })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Id, r => r.TagId);
    }
}
