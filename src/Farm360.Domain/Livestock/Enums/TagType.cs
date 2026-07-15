namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// Physical tag type used to identify the animal.
/// Constitution §4.2: UQ_Animals_TenantId_TagId — unique per tenant, not globally.
/// </summary>
public enum TagType
{
    /// <summary>Handwritten or printed label — most common in Bangladesh.</summary>
    Manual = 1,

    /// <summary>Physical plastic ear tag with embossed ID.</summary>
    EarTag = 2,

    /// <summary>Radio-frequency identification chip (Phase 2 IoT feature).</summary>
    Rfid = 3
}
