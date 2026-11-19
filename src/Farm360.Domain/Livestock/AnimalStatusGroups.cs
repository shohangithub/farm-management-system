using Farm360.Domain.Livestock.Enums;

namespace Farm360.Domain.Livestock;

/// <summary>
/// Shared groupings of <see cref="AnimalStatus"/> so every head count in the system means the
/// same thing. "On farm" = the animal is physically present and is being fed/cared for: Active
/// or Quarantined. Sold, slaughtered, dead and transferred animals are history, not herd.
///
/// FARM RULES (apply everywhere animals are counted, listed for new work, or recorded against):
///   - Exit date = SaleDate (Sold) or DeathDate (Dead) — see <see cref="Animal.ExitDate"/>.
///     Slaughtered / Transferred exist in the enum but no command sets them; they are treated as
///     having left the farm too (with no known exit date).
///   - On farm on day D = AcquisitionDate &lt;= D and (no exit date or D &lt; exit date).
///     Quarantined animals are on the farm.
///   - History is never deleted: records dated before the exit stay; nothing new may be recorded
///     for dates on/after the exit except corrections to existing records.
/// </summary>
public static class AnimalStatusGroups
{
    /// <summary>Statuses of animals present on the farm. Usable in EF queries via <c>Contains</c>.</summary>
    public static readonly AnimalStatus[] OnFarm = [AnimalStatus.Active, AnimalStatus.Quarantined];

    /// <summary>Statuses of animals that have left the farm (sold, slaughtered, dead, transferred out).</summary>
    public static readonly AnimalStatus[] LeftFarm =
        [AnimalStatus.Sold, AnimalStatus.Slaughtered, AnimalStatus.Dead, AnimalStatus.Transferred];

    /// <summary>True when an animal with this status is present on the farm.</summary>
    public static bool IsOnFarm(AnimalStatus status) =>
        status is AnimalStatus.Active or AnimalStatus.Quarantined;

    /// <summary>True when an animal with this status has left the farm (the inverse of <see cref="IsOnFarm"/>).</summary>
    public static bool HasLeftFarm(AnimalStatus status) => !IsOnFarm(status);
}
