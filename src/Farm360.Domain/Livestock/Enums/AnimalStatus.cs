namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// Operational lifecycle status of an animal.
/// Constitution §2.4: State transitions enforced inside domain entity methods.
/// Invalid transitions throw InvalidAnimalStateTransitionException.
/// F360-MTA-2026-001: IsDeleted=1 is the soft-delete state (separate from status).
/// </summary>
public enum AnimalStatus
{
    /// <summary>Animal is on-farm and operational. Default state after registration.</summary>
    Active = 1,

    /// <summary>Animal is healthy but restricted — under observation or legal hold.</summary>
    Quarantined = 2,

    /// <summary>Animal has been sold and has left the farm.</summary>
    Sold = 3,

    /// <summary>Animal was slaughtered on-farm (e.g. Eid ul-Adha).</summary>
    Slaughtered = 4,

    /// <summary>Animal died — cause recorded in MortalityRecord.</summary>
    Dead = 5,

    /// <summary>Animal was transferred to another farm or shed.</summary>
    Transferred = 6
}
