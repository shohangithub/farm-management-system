namespace Farm360.Domain.Dashboard;

/// <summary>
/// Raw counts behind the "Get your farm ready" checklist for one farm. Each value is a cheap
/// COUNT over the current tenant's data; the application layer turns them into setup steps.
/// </summary>
public sealed record FarmSetupCounts(
    int Sheds,
    int Breeds,
    int Animals,
    int WeightRecords,
    int InventoryItemsInStock,
    int FeedItemsInStock,
    int FeedRecipes,
    int FeedingRuleSets,
    int ActiveFeedingPlans,
    bool AccountingSettingsSaved,
    int TeamMembers,
    int PendingInvitations,
    bool HasLocation);
