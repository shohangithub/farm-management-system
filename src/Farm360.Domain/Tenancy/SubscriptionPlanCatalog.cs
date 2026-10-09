using System.Collections.Generic;

namespace Farm360.Domain.Tenancy;

/// <summary>Quotas and prices for one subscription tier.</summary>
public sealed record SubscriptionPlanPricing(
    int MaxUsers,
    int MaxFarms,
    int MaxAnimals,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    decimal OneTimePrice);

/// <summary>
/// The single source of truth for what each subscription tier costs and allows.
/// </summary>
/// <remarks>
/// Before this existed, the same four tiers' prices and quotas were hardcoded three times
/// (the plans catalog query, the subscribe command's price switch, and
/// <see cref="Tenant.SetQuotasForTier"/>) -- any future price change had to be made in three
/// places and stay in sync by discipline alone. Now all three read from here.
/// </remarks>
public static class SubscriptionPlanCatalog
{
    private static readonly Dictionary<SubscriptionTier, SubscriptionPlanPricing> Plans =
        new Dictionary<SubscriptionTier, SubscriptionPlanPricing>
        {
            [SubscriptionTier.Starter] = new(
                MaxUsers: 3, MaxFarms: 1, MaxAnimals: 100,
                MonthlyPrice: 1500m, YearlyPrice: 15000m, OneTimePrice: 35000m),

            [SubscriptionTier.Standard] = new(
                MaxUsers: 10, MaxFarms: 5, MaxAnimals: 500,
                MonthlyPrice: 3500m, YearlyPrice: 35000m, OneTimePrice: 75000m),

            [SubscriptionTier.Professional] = new(
                MaxUsers: 50, MaxFarms: 20, MaxAnimals: 5000,
                MonthlyPrice: 7500m, YearlyPrice: 75000m, OneTimePrice: 160000m),

            [SubscriptionTier.Enterprise] = new(
                MaxUsers: int.MaxValue, MaxFarms: int.MaxValue, MaxAnimals: int.MaxValue,
                MonthlyPrice: 15000m, YearlyPrice: 150000m, OneTimePrice: 350000m),
        };

    public static SubscriptionPlanPricing For(SubscriptionTier tier) =>
        Plans.TryGetValue(tier, out var pricing) ? pricing : Plans[SubscriptionTier.Starter];

    public static decimal PriceFor(SubscriptionTier tier, SubscriptionBillingCycle cycle)
    {
        var pricing = For(tier);
        return cycle switch
        {
            SubscriptionBillingCycle.Yearly => pricing.YearlyPrice,
            SubscriptionBillingCycle.OneTime => pricing.OneTimePrice,
            _ => pricing.MonthlyPrice,
        };
    }
}
