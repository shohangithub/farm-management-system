using Farm360.Application.Demo;

namespace Farm360.Application.Livestock;

/// <summary>
/// The breed categories the breed screens offer. Seeded breeds (the default list and the demo farm)
/// use only these, so filters and badges group them correctly.
/// </summary>
public static class BreedCategories
{
    public const string Indigenous = "Indigenous";
    public const string Exotic = "Exotic";
    public const string Crossbred = "Crossbred";

    public static IReadOnlyList<string> All { get; } = [Indigenous, Exotic, Crossbred];

    public static bool IsKnown(string? category) =>
        category is not null && All.Contains(category, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One-off, idempotent data fix: older demo seeds stored breeds with a species as the category
/// ("Cattle", "Goat"). Only rows that carry the seed's exact fingerprint (name, origin and the
/// description the seeder writes) and a category outside <see cref="BreedCategories.All"/> are
/// corrected, so breeds a tenant created or re-categorised themselves are never touched.
/// </summary>
public static class SeedBreedCategoryFix
{
    /// <summary>The columns the fix needs from a stored breed.</summary>
    public sealed record StoredBreed(Guid Id, string Name, string Category, string Origin, string Description);

    /// <summary>Names worth loading from the database (every seed breed name).</summary>
    public static IReadOnlyList<string> SeedBreedNames(IEnumerable<DemoBreedSpec>? seeds = null) =>
        (seeds ?? DemoSeedPlan.BreedSpecs).Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Breed id → corrected category, for the rows that need it. Empty when nothing to fix.</summary>
    public static IReadOnlyDictionary<Guid, string> Plan(IEnumerable<StoredBreed> rows, IEnumerable<DemoBreedSpec>? seeds = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var specs = (seeds ?? DemoSeedPlan.BreedSpecs).ToList();

        var fixes = new Dictionary<Guid, string>();
        foreach (var row in rows)
        {
            if (BreedCategories.IsKnown(row.Category))
                continue; // already a UI category, whoever set it

            var spec = specs.FirstOrDefault(s =>
                string.Equals(s.Name, row.Name, StringComparison.Ordinal) &&
                string.Equals(s.Origin, row.Origin, StringComparison.Ordinal) &&
                string.Equals(s.SeedDescription, row.Description, StringComparison.Ordinal));

            if (spec is not null && BreedCategories.IsKnown(spec.Category))
                fixes[row.Id] = spec.Category;
        }

        return fixes;
    }
}
