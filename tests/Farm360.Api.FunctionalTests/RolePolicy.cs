using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Api.Authorization;
using Farm360.Application.Common.Interfaces;
using Farm360.Persistence.Seed;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Farm360.Api.FunctionalTests;

/// <summary>
/// Runs a route's real permission policy through the real <see cref="PermissionHandler"/> for a system
/// role, using the permissions the seeder gives that role.
/// </summary>
internal static class RolePolicy
{
    internal static string PolicyOf(string key)
    {
        var endpoints = EndpointRoutingTests.BuildEndpoints()
            .OfType<RouteEndpoint>()
            .Where(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"])
                .Any(m => $"{m} /{(e.RoutePattern.RawText ?? string.Empty).Trim('/')}".Equals(key, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        endpoints.Should().ContainSingle($"route {key} should exist");
        return endpoints[0].Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Select(a => a.Policy)
            .Single(p => p is not null && p.StartsWith(EndpointPermissionExtensions.PermissionPolicyPrefix, StringComparison.Ordinal))!;
    }

    internal static async Task<bool> IsAllowedAsync(string route, Guid roleId)
    {
        var policy = PolicyOf(route);
        var requirement = new PermissionRequirement(policy[EndpointPermissionExtensions.PermissionPolicyPrefix.Length..]);
        var codes = DataSeeder.SystemRolePermissionCodes(roleId);

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("perms", string.Join(',', codes))], authenticationType: "Test"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);

        var handler = new PermissionHandler(new FakePermissions(codes), new FakeUser(), NullLogger<PermissionHandler>.Instance);
        await handler.HandleAsync(context);
        return context.HasSucceeded;
    }

    private sealed class FakePermissions(IReadOnlySet<string> codes) : IPermissionService
    {
        public Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permissionCode, CancellationToken cancellationToken = default)
            => Task.FromResult(codes.Contains(permissionCode));
        public Task<IReadOnlyList<string>> GetPermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(codes.ToList());
        public Task InvalidatePermissionCacheAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? TenantId { get; } = Guid.NewGuid();
        public string? Role => "Worker";
        public IReadOnlyList<Guid>? AssignedFarmIds => null;
        public int? TokenVersion => 1;
        public string? SubscriptionTier => "Professional";
        public bool IsAuthenticated => true;
        public bool IsSystemUser => false;
    }
}
