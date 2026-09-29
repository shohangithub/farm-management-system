using Farm360.Application.Common.Interfaces;
using Farm360.Application.Subscriptions.Commands;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Farm360.Application.UnitTests.Subscriptions;

public sealed class SubscriptionCommandHandlerTests
{
    private readonly Mock<ITenantRepository> _tenantRepoMock = new();
    private readonly Mock<ITenantSubscriptionRepository> _subscriptionRepoMock = new();
    private readonly Mock<ICurrentUserService> _currentUserServiceMock = new();
    private readonly Mock<ITenantService> _tenantServiceMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task StartTenantTrial_ValidDays_ShouldActivateTrialAndReturnDto(int days)
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create("Green Farm", "green-farm", SubscriptionTier.Starter);

        _currentUserServiceMock.Setup(s => s.TenantId).Returns(tenantId);
        _tenantRepoMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepoMock.Setup(r => r.GetTenantUsageCountsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 1, 10));

        var handler = new StartTenantTrialCommandHandler(
            _tenantRepoMock.Object,
            _subscriptionRepoMock.Object,
            _currentUserServiceMock.Object,
            _tenantServiceMock.Object,
            _cacheServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new StartTenantTrialCommand(days), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsTrial.Should().BeTrue();
        result.TrialDays.Should().Be(days);
        result.Status.Should().Be("Active");
        result.BillingCycle.Should().Be("Trial");
        result.TrialDaysRemaining.Should().Be(days);

        _tenantRepoMock.Verify(r => r.Update(tenant), Times.Once);
        _subscriptionRepoMock.Verify(r => r.AddAsync(It.Is<TenantSubscriptionRecord>(s => s.PaymentMethod == "Free Trial"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubscribeTenant_Monthly_ShouldActivateAndRecordPayment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create("Green Farm", "green-farm", SubscriptionTier.Starter);

        _currentUserServiceMock.Setup(s => s.TenantId).Returns(tenantId);
        _tenantRepoMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepoMock.Setup(r => r.GetTenantUsageCountsAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 2, 80));

        var handler = new SubscribeTenantCommandHandler(
            _tenantRepoMock.Object,
            _subscriptionRepoMock.Object,
            _currentUserServiceMock.Object,
            _tenantServiceMock.Object,
            _cacheServiceMock.Object,
            _unitOfWorkMock.Object);

        var command = new SubscribeTenantCommand(
            Tier: SubscriptionTier.Standard,
            BillingCycle: SubscriptionBillingCycle.Monthly,
            PaymentMethod: "bKash",
            PaymentReference: "TRX-987654"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Tier.Should().Be("Standard");
        result.BillingCycle.Should().Be("Monthly");
        result.IsTrial.Should().BeFalse();
        result.Status.Should().Be("Active");

        _tenantRepoMock.Verify(r => r.Update(tenant), Times.Once);
        _subscriptionRepoMock.Verify(r => r.AddAsync(It.Is<TenantSubscriptionRecord>(s => s.Amount == 3500m && s.PaymentMethod == "bKash"), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
