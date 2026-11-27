using System;
using System.Threading.Tasks;
using Farm360.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>PUT /farms/{farmId}/location (Weather page "Set location") needs farms.edit, like the farm form.</summary>
public class FarmLocationEndpointTests
{
    private const string Route = "PUT /api/v1/farms/{farmId:guid}/location";

    [Fact]
    public void RequiresFarmsEdit()
    {
        RolePolicy.PolicyOf(Route).Should().Be("Permission:farms.edit");
    }

    [Fact]
    public async Task OwnerAndFarmManagerMaySetTheLocation()
    {
        (await RolePolicy.IsAllowedAsync(Route, SystemRoleIds.Owner)).Should().BeTrue();
        (await RolePolicy.IsAllowedAsync(Route, SystemRoleIds.FarmManager)).Should().BeTrue();
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Worker")]
    [InlineData("Veterinarian")]
    public async Task OtherRolesAreRefused(string role)
    {
        var roleId = role switch
        {
            "Viewer" => SystemRoleIds.Viewer,
            "Worker" => SystemRoleIds.Worker,
            _ => SystemRoleIds.Veterinarian,
        };
        (await RolePolicy.IsAllowedAsync(Route, roleId)).Should().BeFalse();
    }
}
