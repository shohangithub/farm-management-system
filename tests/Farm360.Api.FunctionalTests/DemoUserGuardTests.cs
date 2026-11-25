using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Farm360.Api.Middleware;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Demo;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Farm360.Api.FunctionalTests;

/// <summary>The shared demo account is read-only on every route, including account and billing routes.</summary>
public class DemoUserGuardTests
{
    [Theory]
    [InlineData("POST", "/api/v1/subscriptions/subscribe")]
    [InlineData("POST", "/api/v1/subscriptions/checkout/initiate")]
    [InlineData("POST", "/api/v1/subscriptions/start-trial")]
    [InlineData("POST", "/api/v1/tenants/onboard")]
    [InlineData("POST", "/api/v1/auth/change-password")]
    [InlineData("POST", "/api/v1/auth/logout-all")]
    [InlineData("PUT", "/api/v1/users/profile")]
    [InlineData("POST", "/api/v1/livestock/animals")]
    [InlineData("DELETE", "/api/v1/livestock/animals/7f0c1a52-0000-0000-0000-000000000001")]
    [InlineData("GET", "/api/v1/subscriptions/checkout/success")]
    public async Task DemoUserWritesAndBillingAreRefused(string method, string path)
    {
        var (status, nextCalled) = await InvokeAsync(DemoConstants.ViewerUserId, method, path);

        status.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Theory]
    [InlineData("GET", "/api/v1/farms/7f0c1a52-0000-0000-0000-000000000001/dashboard")]
    [InlineData("GET", "/api/v1/auth/me")]
    [InlineData("POST", "/api/v1/auth/logout")]
    [InlineData("POST", "/api/v1/reports/animal-summary/run")]
    [InlineData("POST", "/api/v1/reports/animal-summary/export/pdf")]
    [InlineData("POST", "/api/v1/intelligence/projections/calculate")]
    [InlineData("POST", "/api/v1/feeding/plans/preview")]
    public async Task DemoUserReadsAndReadOnlyCalculationsAreAllowed(string method, string path)
    {
        var (_, nextCalled) = await InvokeAsync(DemoConstants.ViewerUserId, method, path);

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("POST", "/api/v1/intelligence/projections/scenarios")]
    [InlineData("POST", "/api/v1/feeding/plans/assign")]
    [InlineData("POST", "/api/v1/reports/animal-summary/schedule")]
    public async Task DemoUserLookAlikeWriteRoutesAreRefused(string method, string path)
    {
        var (status, nextCalled) = await InvokeAsync(DemoConstants.ViewerUserId, method, path);

        status.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task OtherUsersAreNotAffected()
    {
        var (_, nextCalled) = await InvokeAsync(Guid.NewGuid(), "POST", "/api/v1/subscriptions/subscribe");

        nextCalled.Should().BeTrue();
    }

    private static async Task<(int Status, bool NextCalled)> InvokeAsync(Guid userId, string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        var nextCalled = false;
        var middleware = new DemoUserGuardMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, new FakeUser(userId));

        return (context.Response.StatusCode, nextCalled);
    }

    private sealed class FakeUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId { get; } = userId;
        public Guid? TenantId { get; } = Guid.NewGuid();
        public string? Role => "Viewer";
        public IReadOnlyList<Guid>? AssignedFarmIds => null;
        public int? TokenVersion => 1;
        public string? SubscriptionTier => "Professional";
        public bool IsAuthenticated => true;
        public bool IsSystemUser => false;
    }
}
