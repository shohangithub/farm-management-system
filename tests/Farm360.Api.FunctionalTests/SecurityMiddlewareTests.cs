using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Api.Authorization;
using Farm360.Api.Middleware;
using Farm360.Application.Common.Interfaces;
using Farm360.Identity.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>Unit-level tests of the security middleware and hooks (no database, no Docker).</summary>
public class SecurityMiddlewareTests
{
    // ── Farm access ──────────────────────────────────────────────────────────

    [Fact]
    public async Task FarmAccessRouteFarmOutsideAllowedFarmsReturns403()
    {
        var farmId = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.RouteValues["farmId"] = farmId.ToString();
        var nextCalled = false;
        var middleware = new FarmAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FakeCurrentUser { IsAuthenticated = true }, new FakeFarmAccess(allowed: false));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task FarmAccessQueryFarmOutsideAllowedFarmsReturns403()
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?farmId={Guid.NewGuid()}");
        var middleware = new FarmAccessMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, new FakeCurrentUser { IsAuthenticated = true }, new FakeFarmAccess(allowed: false));

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task FarmAccessAllowedFarmCallsNext()
    {
        var context = new DefaultHttpContext();
        context.Request.RouteValues["farmId"] = Guid.NewGuid().ToString();
        var nextCalled = false;
        var middleware = new FarmAccessMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FakeCurrentUser { IsAuthenticated = true }, new FakeFarmAccess(allowed: true));

        nextCalled.Should().BeTrue();
    }

    // ── Tenant resolution ───────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/farms/00000000-0000-0000-0000-000000000001/finance/transactions")]
    [InlineData("/api/v1/livestock/animals")]
    public async Task UserWithoutTenantIsForbiddenOnTenantScopedRoutes(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var tenantService = new TenantService();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context, tenantService,
            new FakeCurrentUser { IsAuthenticated = true, TenantId = Guid.Empty }, new FakeCache());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
        tenantService.SubscriptionTier.Should().NotBe("Enterprise");
    }

    [Theory]
    [InlineData("/api/v1/tenants/onboard")]
    [InlineData("/api/v1/auth/me")]
    public async Task UserWithoutTenantCanReachOnboardingRoutesWithoutATier(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var tenantService = new TenantService();
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context, tenantService,
            new FakeCurrentUser { IsAuthenticated = true, TenantId = Guid.Empty }, new FakeCache());

        nextCalled.Should().BeTrue();
        tenantService.SubscriptionTier.Should().Be("None");
        tenantService.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownTenantIsRejectedNotTreatedAsActive()
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        context.Request.Path = "/api/v1/livestock/animals";
        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }, NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context, new TenantService(),
            new FakeCurrentUser { IsAuthenticated = true, TenantId = Guid.NewGuid() }, new FakeCache());

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    // ── Uploads ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/uploads/animals/x.html", 404)]
    [InlineData("/uploads/animals/x.svg", 404)]
    [InlineData("/uploads/animals/x", 404)]
    [InlineData("/uploads/animals/x.png", 200)]
    [InlineData("/uploads/organizations/x.JPG", 200)]
    public async Task UploadsGuardOnlyServesImages(string path, int expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        var middleware = new UploadsGuardMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(expected);
    }

    // ── Token version ───────────────────────────────────────────────────────

    [Fact]
    public async Task TokenVersionStaleFailsAuthentication()
    {
        var userId = Guid.NewGuid();
        var context = BuildTokenContext(userId, tokenVersion: 1, currentVersion: 2);

        await TokenVersionCheck.ValidateAsync(context);

        context.Result.Should().NotBeNull();
        context.Result!.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task TokenVersionCurrentPasses()
    {
        var context = BuildTokenContext(Guid.NewGuid(), tokenVersion: 3, currentVersion: 3);

        await TokenVersionCheck.ValidateAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task TokenVersionMissingClaimFailsAuthentication()
    {
        var context = BuildTokenContext(Guid.NewGuid(), tokenVersion: null, currentVersion: 1);

        await TokenVersionCheck.ValidateAsync(context);

        context.Result!.Failure.Should().NotBeNull();
    }

    // ── Permission mapping ──────────────────────────────────────────────────

    [Theory]
    [InlineData("GET", "v")]
    [InlineData("POST", "c")]
    [InlineData("PUT", "e")]
    [InlineData("PATCH", "e")]
    [InlineData("DELETE", "d")]
    public void PermissionForMethodsMapsVerbs(string method, string expected) =>
        EndpointPermissionExtensions.Policy(expected).Should().Be(
            EndpointPermissionExtensions.Policy(
                InvokePermissionForMethods([method])));

    private static string InvokePermissionForMethods(string[] methods)
    {
        // Internal helper; exercised through reflection to keep it non-public.
        var method = typeof(EndpointPermissionExtensions).GetMethod(
            "PermissionForMethods", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [methods, "v", "c", "e", "d"])!;
    }

    private static TokenValidatedContext BuildTokenContext(Guid userId, int? tokenVersion, int currentVersion)
    {
        var services = new ServiceCollection()
            .AddSingleton<ITokenVersionValidator>(new FakeTokenVersionValidator(currentVersion))
            .BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (tokenVersion is { } tv)
        {
            claims.Add(new Claim("tv", tv.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return new TokenValidatedContext(
            http,
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")),
        };
    }

    // ── Fakes ────────────────────────────────────────────────────────────────

    private sealed class FakeTokenVersionValidator(int current) : ITokenVersionValidator
    {
        public Task<bool> IsCurrentAsync(Guid userId, int tokenVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(tokenVersion == current);

        public void Invalidate(Guid userId) { }
    }

    private sealed class FakeFarmAccess(bool allowed) : IFarmAccessService
    {
        public Task<bool> CanAccessFarmAsync(Guid farmId, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
        public Task<bool> CanAccessAnimalAsync(Guid animalId, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
        public Task<bool> CanAccessBatchAsync(Guid batchId, CancellationToken cancellationToken = default) => Task.FromResult(allowed);
    }

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public Guid? UserId { get; init; } = Guid.NewGuid();
        public Guid? TenantId { get; init; }
        public string? Role { get; init; }
        public IReadOnlyList<Guid>? AssignedFarmIds { get; init; }
        public int? TokenVersion { get; init; }
        public string? SubscriptionTier { get; init; }
        public bool IsAuthenticated { get; init; }
        public bool IsSystemUser { get; init; }
    }

    private sealed class FakeCache : ICacheService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
        public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
