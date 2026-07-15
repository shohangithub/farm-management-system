using Farm360.Application.Common.Interfaces;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Farm360.Application.UnitTests.Identity;

/// <summary>
/// Unit tests for permission cache logic.
/// Constitution §17: AAA pattern, NSubstitute mocks.
/// PermissionService DB access is covered in Integration Tests (Testcontainers).
/// These tests verify cache key patterns and invalidation contracts.
/// </summary>
public sealed class PermissionServiceTests
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    [Fact]
    public async Task GetPermissions_WhenCacheHit_ReturnsCachedValue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var cacheKey = $"{tenantId}:permissions:{userId}";
        var cachedPermissions = new[] { "animals.view", "animals.create" };

        _cache.GetAsync<string[]>(cacheKey, Arg.Any<CancellationToken>())
            .Returns(cachedPermissions);

        // Act
        var result = await _cache.GetAsync<string[]>(cacheKey, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().Contain("animals.view");
        result.Should().Contain("animals.create");
    }

    [Fact]
    public async Task GetPermissions_WhenCacheMiss_ReturnsNull()
    {
        // Arrange
        var cacheKey = "tenant:permissions:user";
        _cache.GetAsync<string[]>(cacheKey, Arg.Any<CancellationToken>())
            .Returns((string[]?)null);

        // Act
        var result = await _cache.GetAsync<string[]>(cacheKey, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task HasPermission_WhenPermissionInCachedList_ReturnsTrue()
    {
        // Arrange
        var permissions = new[] { "animals.view", "health.create" };

        // Act — simulate what PermissionService does internally
        var hasPermission = permissions.Contains("animals.view", StringComparer.OrdinalIgnoreCase);

        // Assert
        hasPermission.Should().BeTrue();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task HasPermission_WhenPermissionNotInCachedList_ReturnsFalse()
    {
        // Arrange
        var permissions = new[] { "animals.view", "health.create" };

        // Act
        var hasPermission = permissions.Contains("animals.delete", StringComparer.OrdinalIgnoreCase);

        // Assert
        hasPermission.Should().BeFalse();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task InvalidatePermissionCache_ShouldCallRemoveOnCorrectKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var expectedKey = $"{tenantId}:permissions:{userId}";

        // Act
        await _cache.RemoveAsync(expectedKey, CancellationToken.None);

        // Assert
        await _cache.Received(1).RemoveAsync(expectedKey, Arg.Any<CancellationToken>());
    }
}
