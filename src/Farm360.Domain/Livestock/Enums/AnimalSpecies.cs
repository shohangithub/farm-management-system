namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// Species of livestock. Governs breed options, gestation periods,
/// vaccination protocols, and feed formula categories.
/// PVD §9 Module 2: Cattle, Goat supported in MVP; Poultry in Phase 2.
/// </summary>
public enum AnimalSpecies
{
    /// <summary>Beef/fattening cattle — Shahibal, Brahman, crossbreeds.</summary>
    CattleBeef = 1,

    /// <summary>Dairy cattle — Holstein-Friesian, Sindhi, Sahiwal.</summary>
    CattleDairy = 2,

    /// <summary>Goat — Black Bengal, Jamnapari, Boer.</summary>
    Goat = 3,

    /// <summary>Sheep.</summary>
    Sheep = 4
}
