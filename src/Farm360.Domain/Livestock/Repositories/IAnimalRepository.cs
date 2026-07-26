using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock.Repositories;

/// <summary>
/// Repository contract for the Animal aggregate.
/// Constitution §2.1 Clean Architecture: Interface defined in Domain; implementation in Persistence.
/// Only aggregate roots have repositories — WeightRecord, BreedingRecord, AnimalPhoto do not.
///
/// All methods are automatically filtered by the EF Core Global Query Filter:
///   WHERE TenantId = @currentTenantId AND IsDeleted = 0
/// No method on this interface should accept a tenantId parameter — it is implicit.
/// </summary>
public interface IAnimalRepository
{
    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the animal by ID, including all child collections.
    /// Returns null if not found or not in current tenant scope.
    /// </summary>
    Task<Animal?> GetByIdAsync(Guid animalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the animal by ID with only weight records loaded.
    /// Optimized for weight history page.
    /// </summary>
    Task<Animal?> GetByIdWithWeightsAsync(Guid animalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a TagId already exists for any animal in the current tenant.
    /// Used by FluentValidation async uniqueness validator.
    /// Excludes the provided animalId — allows editing an animal without tag collision with itself.
    /// </summary>
    Task<bool> TagExistsAsync(string tagId, Guid? excludeAnimalId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns paginated list of animals matching the filter criteria.
    /// All parameters are optional — null means "no filter on this field".
    /// </summary>
    Task<(IReadOnlyList<Animal> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Guid? farmId = null,
        Guid? shedId = null,
        Guid? penId = null,
        AnimalSpecies? species = null,
        AnimalSex? sex = null,
        AnimalStatus? status = null,
        string? searchTerm = null,
        string? sortBy = null,
        bool sortDescending = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of active (non-deleted, non-disposed) animals in the tenant.
    /// Used by subscription limit checks (SubscriptionLimitService).
    /// </summary>
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);

    // ── Commands ──────────────────────────────────────────────────────────────

    void Add(Animal animal);
    void Update(Animal animal);

    /// <summary>
    /// Soft-deletes the animal (sets IsDeleted = true).
    /// Never hard-deletes — Constitution §12: Hard DELETE never permitted.
    /// </summary>
    void SoftDelete(Animal animal, Guid deletedBy);
}
