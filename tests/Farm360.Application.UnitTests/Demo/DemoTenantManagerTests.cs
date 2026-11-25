using Farm360.Application.Common.Interfaces;
using Farm360.Application.Demo;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Farm360.Application.UnitTests.Demo;

/// <summary>The demo login: only the fixed demo Viewer, only when enabled and ready.</summary>
public sealed class DemoTenantManagerTests
{
    private static readonly Guid DemoTenantId = Guid.NewGuid();

    private readonly Mock<IDemoTenantStore> _store = new();
    private readonly Mock<IDemoAccountService> _accounts = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly Mock<ITenantMembershipService> _memberships = new();
    private readonly Mock<IFarmClock> _clock = new();

    private DemoTenantManager CreateManager(bool enabled) => new(
        Options.Create(new DemoOptions { Enabled = enabled }),
        _store.Object,
        _accounts.Object,
        _auth.Object,
        _memberships.Object,
        new DemoSeeder(Mock.Of<IServiceScopeFactory>(), NullLogger<DemoSeeder>.Instance),
        new DemoTenantPurger(_store.Object, Mock.Of<IFileStorageService>(), NullLogger<DemoTenantPurger>.Instance),
        _clock.Object,
        NullLogger<DemoTenantManager>.Instance);

    private void DemoIsReady(string role = "Viewer", bool safeViewer = true)
    {
        _store.Setup(s => s.GetPublishedTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(DemoTenantId);
        _memberships.Setup(m => m.GetActiveMembershipAsync(DemoConstants.ViewerUserId, DemoTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantMembership(DemoTenantId, role));
        _accounts.Setup(a => a.IsSafeViewerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(safeViewer);
        _auth.Setup(a => a.IssueSessionAsync(DemoConstants.ViewerUserId, DemoTenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse("access", "refresh", 900, "session"));
    }

    [Fact]
    public async Task Login_WhenDemoIsDisabled_IsRefused_EvenIfADemoTenantExists()
    {
        DemoIsReady();

        var result = await CreateManager(enabled: false).LoginAsync(CancellationToken.None);

        result.Should().BeNull();
        _auth.Verify(a => a.IssueSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Status_WhenDemoIsDisabled_IsNotEnabledAndNotReady()
    {
        DemoIsReady();

        var status = await CreateManager(enabled: false).GetStatusAsync(CancellationToken.None);

        status.Should().Be(new DemoStatusDto(false, false));
    }

    [Fact]
    public async Task Login_WhenNoDemoTenantIsReady_IsRefused()
    {
        _store.Setup(s => s.GetPublishedTenantIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        var manager = CreateManager(enabled: true);

        (await manager.LoginAsync(CancellationToken.None)).Should().BeNull();
        (await manager.GetStatusAsync(CancellationToken.None)).Should().Be(new DemoStatusDto(true, false));
        _auth.Verify(a => a.IssueSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WhenReady_IssuesASessionForTheDemoViewerOnly()
    {
        DemoIsReady();

        var result = await CreateManager(enabled: true).LoginAsync(CancellationToken.None);

        result.Should().NotBeNull();
        _auth.Verify(a => a.IssueSessionAsync(DemoConstants.ViewerUserId, DemoTenantId, It.IsAny<CancellationToken>()), Times.Once);
        _auth.Verify(a => a.IssueSessionAsync(It.Is<Guid>(id => id != DemoConstants.ViewerUserId), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("FarmManager", true)]
    [InlineData("Viewer", false)]
    public async Task Login_IsRefused_WhenTheDemoAccountIsNotAPlainViewer(string role, bool safeViewer)
    {
        DemoIsReady(role, safeViewer);

        var result = await CreateManager(enabled: true).LoginAsync(CancellationToken.None);

        result.Should().BeNull();
        _auth.Verify(a => a.IssueSessionAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSeeded_WhenDisabled_DoesNothing()
    {
        await CreateManager(enabled: false).EnsureSeededAsync(CancellationToken.None);
        await CreateManager(enabled: false).RebuildIfEnabledAsync(CancellationToken.None);

        _accounts.Verify(a => a.CreateOwnerUserAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _store.Verify(s => s.CreateTenantAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureSeeded_WhenADemoIsAlreadyReady_DoesNotBuildAnother()
    {
        DemoIsReady();

        await CreateManager(enabled: true).EnsureSeededAsync(CancellationToken.None);

        _store.Verify(s => s.CreateTenantAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void OnlyTheFixedDemoAccountIsTheDemoViewer()
    {
        DemoConstants.IsDemoViewer(DemoConstants.ViewerUserId).Should().BeTrue();
        DemoConstants.IsDemoViewer(Guid.NewGuid()).Should().BeFalse();
        DemoConstants.IsDemoViewer(null).Should().BeFalse();
    }
}
