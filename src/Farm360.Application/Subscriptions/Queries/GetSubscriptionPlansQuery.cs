using Farm360.Application.Subscriptions.DTOs;
using Farm360.Domain.Tenancy;
using MediatR;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Subscriptions.Queries;

public sealed record SubscriptionCatalogDto(
    IReadOnlyList<SubscriptionPlanDto> Plans,
    IReadOnlyList<TrialOptionDto> TrialOptions);

public sealed record GetSubscriptionPlansQuery : IRequest<SubscriptionCatalogDto>;

internal sealed class GetSubscriptionPlansQueryHandler : IRequestHandler<GetSubscriptionPlansQuery, SubscriptionCatalogDto>
{
    public Task<SubscriptionCatalogDto> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var starter = SubscriptionPlanCatalog.For(SubscriptionTier.Starter);
        var standard = SubscriptionPlanCatalog.For(SubscriptionTier.Standard);
        var professional = SubscriptionPlanCatalog.For(SubscriptionTier.Professional);
        var enterprise = SubscriptionPlanCatalog.For(SubscriptionTier.Enterprise);

        var plans = new List<SubscriptionPlanDto>
        {
            new(
                Tier: "Starter",
                Name: "Starter Farm",
                Description: "Essential operational management for smallholder farms and single-site setups.",
                MonthlyPrice: starter.MonthlyPrice,
                YearlyPrice: starter.YearlyPrice,
                OneTimePrice: starter.OneTimePrice,
                MaxUsers: starter.MaxUsers,
                MaxFarms: starter.MaxFarms,
                MaxAnimals: starter.MaxAnimals,
                Features:
                [
                    "Up to 1 Farm & 100 Animals",
                    "3 User Accounts",
                    "Livestock & Breed Management",
                    "Standard Health & Vaccine Records",
                    "Basic Daily Feeding Logging",
                    "Standard Email Support"
                ],
                IsPopular: false
            ),
            new(
                Tier: "Standard",
                Name: "Standard Commercial",
                Description: "Comprehensive farm optimization for growing livestock businesses and multi-shed setups.",
                MonthlyPrice: standard.MonthlyPrice,
                YearlyPrice: standard.YearlyPrice,
                OneTimePrice: standard.OneTimePrice,
                MaxUsers: standard.MaxUsers,
                MaxFarms: standard.MaxFarms,
                MaxAnimals: standard.MaxAnimals,
                Features:
                [
                    "Up to 5 Farms & 500 Animals",
                    "10 User Accounts with Role Permissions",
                    "Smart Ration & FCR Engine",
                    "Inventory Management & Daily Consumables",
                    "Financial Transactions & Loan Tracking",
                    "Automated Health Alerts & Reports",
                    "Priority WhatsApp & Phone Support"
                ],
                IsPopular: true
            ),
            new(
                Tier: "Professional",
                Name: "Professional Enterprise",
                Description: "Advanced analytics, multiple branches, investor P&L, and full multi-farm governance.",
                MonthlyPrice: professional.MonthlyPrice,
                YearlyPrice: professional.YearlyPrice,
                OneTimePrice: professional.OneTimePrice,
                MaxUsers: professional.MaxUsers,
                MaxFarms: professional.MaxFarms,
                MaxAnimals: professional.MaxAnimals,
                Features:
                [
                    "Up to 20 Farms & 5,000 Animals",
                    "50 User Accounts with Custom Roles",
                    "Full Multi-Branch & Multi-Org Support",
                    "Investor P&L & Share Market Features",
                    "Real-time Executive Dashboard & Intelligence",
                    "Automated Cost Allocation Engines",
                    "24/7 Dedicated Account Manager"
                ],
                IsPopular: false
            ),
            new(
                Tier: "Enterprise",
                Name: "Corporate / Custom",
                Description: "Unlimited scale for agribusiness groups, cooperatives, and government research stations.",
                MonthlyPrice: enterprise.MonthlyPrice,
                YearlyPrice: enterprise.YearlyPrice,
                OneTimePrice: enterprise.OneTimePrice,
                // Displayed as a large round number rather than int.MaxValue (the real enforcement
                // value for "unlimited") -- a progress bar showing "5 / 2147483647" would look broken.
                MaxUsers: 9999,
                MaxFarms: 9999,
                MaxAnimals: 999999,
                Features:
                [
                    "Unlimited Farms, Animals & Users",
                    "Custom SLA & Guaranteed Uptime",
                    "Dedicated Infrastructure / On-Premise option",
                    "Custom Integration & API Gateways",
                    "Custom AI Analytics & Feed Formulation",
                    "Direct Engineer & On-Site Support"
                ],
                IsPopular: false
            )
        };

        var trialOptions = new List<TrialOptionDto>
        {
            new(
                Days: 3,
                Title: "3-Day Fast Track",
                Description: "Quick hands-on test of core animal registration, health records, and feeding logging."
            ),
            new(
                Days: 7,
                Title: "7-Day Full Evaluation (Recommended)",
                Description: "Complete test-drive including multi-user roles, inventory stock, and financial tracking."
            ),
            new(
                Days: 10,
                Title: "10-Day Extended Pilot",
                Description: "In-depth operational pilot with full reporting, batch workflows, and team onboarding."
            )
        };

        return Task.FromResult(new SubscriptionCatalogDto(plans, trialOptions));
    }
}
