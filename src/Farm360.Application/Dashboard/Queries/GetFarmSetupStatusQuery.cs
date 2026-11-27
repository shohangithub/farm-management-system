using Farm360.Application.Common.Interfaces;
using Farm360.Application.Dashboard.DTOs;
using Farm360.Domain.Dashboard.Interfaces;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Farm360.Application.Dashboard.Queries;

/// <summary>The onboarding checklist for one farm, in the order a new customer should work through it.</summary>
public sealed record GetFarmSetupStatusQuery(Guid FarmId) : IRequest<FarmSetupStatusDto>;

internal sealed class GetFarmSetupStatusQueryHandler : IRequestHandler<GetFarmSetupStatusQuery, FarmSetupStatusDto>
{
    private readonly IFarmSetupStatusRepository _repository;
    private readonly ITenantService _tenantService;

    public GetFarmSetupStatusQueryHandler(
        IFarmSetupStatusRepository repository,
        ITenantService tenantService)
    {
        _repository = repository;
        _tenantService = tenantService;
    }

    public async Task<FarmSetupStatusDto> Handle(GetFarmSetupStatusQuery request, CancellationToken cancellationToken)
    {
        var c = await _repository.GetCountsAsync(_tenantService.TenantId, request.FarmId, cancellationToken);

        var teamSize = c.TeamMembers + c.PendingInvitations;
        var steps = new List<FarmSetupStepDto>
        {
            new("sheds", c.Sheds > 0, c.Sheds),
            // Latitude + longitude drive the weather forecast and the farm map.
            new("location", c.HasLocation, c.HasLocation ? 1 : 0),
            new("breeds", c.Breeds > 0, c.Breeds),
            new("animals", c.Animals > 0, c.Animals),
            new("weights", c.WeightRecords > 0, c.WeightRecords),
            // Feeding plans draw from feed stock, so the inventory step needs feed on hand, not just any item.
            new("inventory", c.FeedItemsInStock > 0, c.InventoryItemsInStock),
            new("recipes", c.FeedRecipes > 0, c.FeedRecipes),
            new("feedChart", c.FeedingRuleSets > 0, c.FeedingRuleSets),
            new("plans", c.ActiveFeedingPlans > 0, c.ActiveFeedingPlans),
            new("accounts", c.AccountingSettingsSaved, c.AccountingSettingsSaved ? 1 : 0),
            new("team", c.TeamMembers > 1 || c.PendingInvitations > 0, teamSize),
        };

        var completed = steps.Count(s => s.IsDone);
        return new FarmSetupStatusDto(request.FarmId, completed, steps.Count, completed == steps.Count, steps);
    }
}
