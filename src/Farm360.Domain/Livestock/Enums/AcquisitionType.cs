namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// How the animal was acquired by the farm.
/// Determines whether acquisition cost is purchase price or zero (born on-farm).
/// </summary>
public enum AcquisitionType
{
    /// <summary>Purchased from a market, trader, or another farm.</summary>
    Purchased = 1,

    /// <summary>Born on-farm; acquisition cost = 0 (dam's lactation cost tracked separately).</summary>
    BornOnFarm = 2
}
