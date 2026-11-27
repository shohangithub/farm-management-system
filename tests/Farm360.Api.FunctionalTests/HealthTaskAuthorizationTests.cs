using System;
using System.Threading.Tasks;
using Farm360.Domain.Identity;
using Farm360.Persistence.Seed;
using FluentAssertions;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>
/// Workers do the daily health checklist (give / skip a task) through health.tasks; Viewers stay
/// read-only and rescheduling stays with health.edit. Runs the real policy strings through the real
/// PermissionHandler with each system role's seeded permissions.
/// </summary>
public class HealthTaskAuthorizationTests
{
    private const string Complete = "POST /api/v1/health/tasks/complete";
    private const string Skip = "POST /api/v1/health/tasks/{id:guid}/skip";
    private const string Reschedule = "POST /api/v1/health/tasks/{id:guid}/reschedule";

    [Fact]
    public void HealthTasksPermissionIsSeededForWorkersButNotViewers()
    {
        foreach (var role in new[] { SystemRoleIds.Owner, SystemRoleIds.FarmManager, SystemRoleIds.Veterinarian, SystemRoleIds.Worker })
            DataSeeder.SystemRolePermissionCodes(role).Should().Contain(PermissionConstants.HealthModule.Tasks);

        DataSeeder.SystemRolePermissionCodes(SystemRoleIds.Viewer).Should().NotContain(PermissionConstants.HealthModule.Tasks);
        DataSeeder.SystemRolePermissionCodes(SystemRoleIds.Worker).Should().NotContain(PermissionConstants.HealthModule.Edit);
    }

    [Theory]
    [InlineData(Complete)]
    [InlineData(Skip)]
    public async Task CompleteAndSkipAcceptAWorker(string route)
    {
        (await IsAllowedAsync(route, SystemRoleIds.Worker)).Should().BeTrue();
        (await IsAllowedAsync(route, SystemRoleIds.Veterinarian)).Should().BeTrue();
        (await IsAllowedAsync(route, SystemRoleIds.FarmManager)).Should().BeTrue();
    }

    [Theory]
    [InlineData(Complete)]
    [InlineData(Skip)]
    [InlineData(Reschedule)]
    public async Task HealthTaskWritesRefuseAViewer(string route)
    {
        (await IsAllowedAsync(route, SystemRoleIds.Viewer)).Should().BeFalse();
    }

    [Fact]
    public async Task RescheduleStaysWithHealthEdit()
    {
        PolicyOf(Reschedule).Should().Be("Permission:" + PermissionConstants.HealthModule.Edit);
        (await IsAllowedAsync(Reschedule, SystemRoleIds.Worker)).Should().BeFalse();
        (await IsAllowedAsync(Reschedule, SystemRoleIds.Veterinarian)).Should().BeTrue();
    }

    private static string PolicyOf(string key) => RolePolicy.PolicyOf(key);

    private static Task<bool> IsAllowedAsync(string route, Guid roleId) => RolePolicy.IsAllowedAsync(route, roleId);
}
