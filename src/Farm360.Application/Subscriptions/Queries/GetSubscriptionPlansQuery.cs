using Farm360.Application.Subscriptions.DTOs;
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
        var plans = new List<SubscriptionPlanDto>
        {
            new(
                Tier: "Starter",
                Name: "Starter Farm",
                Description: "Essential operational management for smallholder farms and single-site setups.",
                MonthlyPrice: 1500m,
                YearlyPrice: 15000m,
                OneTimePrice: 35000m,
                MaxUsers: 3,
                MaxFarms: 1,
                MaxAnimals: 100,
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
                MonthlyPrice: 3500m,
                YearlyPrice: 35000m,
                OneTimePrice: 75000m,
                MaxUsers: 10,
                MaxFarms: 5,
                MaxAnimals: 500,
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
                MonthlyPrice: 7500m,
                YearlyPrice: 75000m,
                OneTimePrice: 160000m,
                MaxUsers: 50,
                MaxFarms: 20,
                MaxAnimals: 5000,
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
                MonthlyPrice: 15000m,
                YearlyPrice: 150000m,
                OneTimePrice: 350000m,
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
