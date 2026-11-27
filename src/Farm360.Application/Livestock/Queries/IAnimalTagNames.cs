namespace Farm360.Application.Livestock.Queries;

/// <summary>
/// Set-based lookup of animal ear-tag numbers, for lists whose rows only hold the animal id
/// (vaccinations, treatments, mortality). One query per page instead of one per animal.
/// Implemented in Persistence; includes animals that have left the farm.
/// </summary>
public interface IAnimalTagNames
{
    /// <summary>Animal id → tag number for the given ids. Unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetTagsAsync(IReadOnlyCollection<Guid> animalIds, CancellationToken cancellationToken = default);
}

public static class AnimalTagNamesExtensions
{
    /// <summary>Tags for the distinct ids in <paramref name="animalIds"/>; empty when there are none or no lookup.</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> TagsForAsync(
        this IAnimalTagNames? lookup, IEnumerable<Guid> animalIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(animalIds);
        var ids = animalIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (lookup is null || ids.Count == 0)
            return new Dictionary<Guid, string>();
        return await lookup.GetTagsAsync(ids, cancellationToken).ConfigureAwait(false);
    }
}
