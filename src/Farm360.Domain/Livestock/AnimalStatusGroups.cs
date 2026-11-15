using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Shared groupings of <see cref="AnimalStatus"/> so every head count in the system means the
/// same thing. "On farm" = the animal is physically present and is being fed/cared for: Active
/// or Quarantined. Sold, slaughtered, dead and transferred animals are history, not herd.
/// </summary>
public static class AnimalStatusGroups
{
    /// <summary>Statuses of animals present on the farm. Usable in EF queries via <c>Contains</c>.</summary>
    public static readonly AnimalStatus[] OnFarm = [AnimalStatus.Active, AnimalStatus.Quarantined];

    /// <summary>True when an animal with this status is present on the farm.</summary>
    public static bool IsOnFarm(AnimalStatus status) =>
        status is AnimalStatus.Active or AnimalStatus.Quarantined;
}
