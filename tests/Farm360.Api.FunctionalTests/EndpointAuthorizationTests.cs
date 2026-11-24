using System;
using System.Collections.Generic;
using System.Linq;
using Farm360.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>
/// Security guard over the endpoint table: every route must either require a permission policy,
/// the PlatformAdmin policy, be explicitly anonymous, or be on the short, reviewed allow-list of
/// routes that are intentionally "any authenticated user".
/// </summary>
public class EndpointAuthorizationTests
{
    /// <summary>
    /// Routes that are deliberately authenticated-only. Each one acts only on the caller's own
    /// identity/tenant and is needed before a user has any tenant permissions. Adding to this list
    /// needs a security review.
    /// </summary>
    private static readonly HashSet<string> AuthenticatedOnlyAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST /api/v1/auth/logout",          // revokes only the caller's own refresh token
        "POST /api/v1/auth/logout-all",      // revokes only the caller's own sessions/tokens
        "GET /api/v1/auth/me",               // caller's own profile
        "GET /api/v1/users/profile",         // caller's own profile
        "PUT /api/v1/users/profile",         // caller's own profile
        "POST /api/v1/auth/change-password", // caller's own password (current password required)
        "POST /api/v1/tenants/onboard",      // tenant-less user creates their first organisation (one per user)
        "GET /api/v1/subscriptions/current", // caller's own tenant status; must work for tenant-less users
        // Geographic reference look-ups used by address pickers across modules; tenant-scoped in
        // LocationRepository and contain no business data.
        "GET /api/v1/locations/countries",
        "GET /api/v1/locations/divisions",
        "GET /api/v1/locations/districts",
        "GET /api/v1/locations/upazilas",
        "GET /api/v1/locations/unions",
        "GET /api/v1/locations/villages",
    };

    /// <summary>
    /// Routes that are deliberately anonymous (AllowAnonymous). Each one is rate-limited and
    /// either authenticates the caller itself or is a public callback/listing. Adding to this
    /// list needs a security review.
    /// </summary>
    private static readonly HashSet<string> AnonymousAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST /api/v1/auth/login",
        "POST /api/v1/auth/refresh",
        "POST /api/v1/auth/register",
        // Password reset via SMS OTP: generic responses (no user enumeration), auth rate limit.
        "POST /api/v1/auth/password/forgot",
        "POST /api/v1/auth/password/verify",
        "POST /api/v1/auth/password/reset",
        // Invitation accept: the single-use token (sent to the invitee) is the credential.
        "POST /api/v1/auth/invitations/preview",
        "POST /api/v1/auth/invitations/accept",
        // Public plan listing and SSLCommerz callbacks (IPN is validated against the gateway).
        "GET /api/v1/subscriptions/plans",
        "POST /api/v1/subscriptions/checkout/ipn",
        "POST /api/v1/subscriptions/checkout/success",
        "POST /api/v1/subscriptions/checkout/fail",
        "POST /api/v1/subscriptions/checkout/cancel",
        // Health probes, deliberately anonymous and exempt from rate limiting: they expose only
        // check names/status/timings (never exception text). /health = /health/live.
        "ANY /health",
        "ANY /health/live",
        "ANY /health/ready",
    };

    internal static readonly string[] HealthRoutes = ["/health", "/health/live", "/health/ready"];

    private static IEnumerable<(string Key, RouteEndpoint Endpoint)> RouteEndpoints() =>
        EndpointRoutingTests.BuildEndpoints()
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"])
                .Select(m => ($"{m} {Normalize(e.RoutePattern.RawText)}", e)));

    private static string Normalize(string? raw) => "/" + (raw ?? string.Empty).Trim('/');

    private static bool HasPolicy(Endpoint e, Func<string, bool> predicate) =>
        e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(a => a.Policy is not null && predicate(a.Policy));

    [Fact]
    public void EveryEndpointRequiresAPermissionOrIsExplicitlyAllowed()
    {
        var unprotected = RouteEndpoints()
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(x => !HasPolicy(x.Endpoint, p =>
                p.StartsWith(EndpointPermissionExtensions.PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase) ||
                p == AuthorizationPolicies.PlatformAdmin))
            .Select(x => x.Key)
            .Where(k => !AuthenticatedOnlyAllowList.Contains(k))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        string.Join(Environment.NewLine, unprotected).Should().BeEmpty(
            "every route must declare a Permission:* or PlatformAdmin policy (or AllowAnonymous). " +
            "Authenticated-only routes let any user of any role reach the module.");
    }

    [Fact]
    public void EveryAnonymousEndpointIsOnTheReviewedAllowList()
    {
        var anonymous = RouteEndpoints()
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(x => x.Key)
            .Where(k => !AnonymousAllowList.Contains(k))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        string.Join(Environment.NewLine, anonymous).Should().BeEmpty(
            "anonymous routes must be reviewed and listed in AnonymousAllowList");
    }

    [Fact]
    public void EveryNonAnonymousEndpointRequiresAuthentication()
    {
        var missing = RouteEndpoints()
            .Where(x => x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Where(x => !x.Endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(x => x.Key)
            .ToList();

        missing.Should().BeEmpty();
    }

    [Fact]
    public void HealthProbesAreAnonymousAndExemptFromRateLimiting()
    {
        foreach (var path in HealthRoutes)
        {
            var endpoints = RouteEndpoints().Where(x => Normalize(x.Endpoint.RoutePattern.RawText) == path).ToList();

            endpoints.Should().NotBeEmpty($"{path} should be mapped");
            endpoints.Should().OnlyContain(x =>
                x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() != null
                && x.Endpoint.Metadata.GetMetadata<DisableRateLimitingAttribute>() != null,
                $"{path} must be reachable by load balancers without a token or a rate-limit budget");
        }
    }

    [Fact]
    public void PlatformAdminRoutesRequirePlatformAdminPolicy()
    {
        var adminRoutes = RouteEndpoints()
            .Where(x => x.Key.Contains("/admin/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        adminRoutes.Should().NotBeEmpty();
        adminRoutes.Should().OnlyContain(
            x => HasPolicy(x.Endpoint, p => p == AuthorizationPolicies.PlatformAdmin)
                 && x.Endpoint.Metadata.GetMetadata<IAllowAnonymous>() == null,
            "cross-tenant admin routes must never be reachable by a normal tenant user");
    }

    [Theory]
    [InlineData("GET /api/v1/farms/{farmId:guid}/finance/transactions", "finance.view")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/transactions", "finance.create")]
    [InlineData("PUT /api/v1/farms/{farmId:guid}/finance/transactions/{id:guid}", "finance.edit")]
    [InlineData("DELETE /api/v1/farms/{farmId:guid}/finance/transactions/{id:guid}", "finance.delete")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/accounting/repair", "finance.admin")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/overhead/allocate", "finance.admin")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/ledger/backfill-manual-animal-expenses", "finance.admin")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/shares/purchase", "finance.admin")]
    [InlineData("POST /api/v1/farms/{farmId:guid}/finance/investors", "finance.admin")]
    [InlineData("GET /api/v1/farms/{farmId:guid}/finance/investors", "finance.view")]
    [InlineData("GET /api/v1/finance/reports/consolidated", "finance.view")]
    [InlineData("GET /api/v1/farms/{farmId:guid}/dashboard", "dashboard.view")]
    [InlineData("GET /api/v1/farms/{farmId:guid}/analytics/breeding", "analytics.view")]
    [InlineData("POST /api/v1/intelligence/projections/calculate", "intelligence.view")]
    [InlineData("GET /api/v1/users/members", "users.view")]
    [InlineData("PUT /api/v1/users/members/{memberId:guid}/role", "users.assignrole")]
    [InlineData("PUT /api/v1/users/members/{memberId:guid}/branch", "users.edit")]
    [InlineData("POST /api/v1/users/members/{memberId:guid}/deactivate", "users.edit")]
    [InlineData("POST /api/v1/users/members/{memberId:guid}/reactivate", "users.edit")]
    [InlineData("DELETE /api/v1/users/members/{memberId:guid}", "users.remove")]
    [InlineData("GET /api/v1/users/invitations", "users.view")]
    [InlineData("POST /api/v1/users/invitations", "users.invite")]
    [InlineData("POST /api/v1/users/invitations/{invitationId:guid}/resend", "users.invite")]
    [InlineData("POST /api/v1/users/invitations/{invitationId:guid}/revoke", "users.invite")]
    [InlineData("GET /api/v1/settings/business-rules", "settings.view")]
    [InlineData("PUT /api/v1/settings/business-rules", "settings.edit")]
    [InlineData("GET /api/v1/documents/sale-invoice/{animalId:guid}", "animals.view")]
    [InlineData("GET /api/v1/documents/payment-receipt/{id:guid}", "finance.view")]
    [InlineData("GET /api/v1/documents/purchase-order/{id:guid}", "inventory.view")]
    public void ModuleRoutesRequireExpectedPermission(string key, string permission)
    {
        var endpoint = RouteEndpoints().Where(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();

        endpoint.Should().NotBeEmpty($"route {key} should exist");
        endpoint.Should().OnlyContain(x => HasPolicy(x.Endpoint, p => p == "Permission:" + permission));
    }
}
