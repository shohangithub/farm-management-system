namespace Farm360.Domain.Livestock.Enums;

/// <summary>
/// Reason for animal disposal (sale, slaughter, death, etc.).
/// Used in disposal events for financial cost posting and analytics.
/// PRD §7: Each disposal type maps to a different FinancialEntry category.
/// </summary>
public enum DisposalReason
{
    Sale = 1,
    Slaughter = 2,
    NaturalDeath = 3,
    Disease = 4,
    Accident = 5,
    Unknown = 6
}
