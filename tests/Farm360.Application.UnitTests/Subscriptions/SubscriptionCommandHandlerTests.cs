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
    public async Task SubscribeTenant_Monthly_ShouldRecordPendingPaymentWithoutActivating()
    {
        // A self-reported reference (bKash/Nagad/card/bank) is unverified -- it must land as
        // Pending and NOT touch the tenant's subscription until an admin confirms it.
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = Tenant.Create("Green Farm", "green-farm", SubscriptionTier.Starter);

        _currentUserServiceMock.Setup(s => s.TenantId).Returns(tenantId);
        _tenantRepoMock.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var handler = new SubscribeTenantCommandHandler(
            _tenantRepoMock.Object,
            _subscriptionRepoMock.Object,
            _currentUserServiceMock.Object,
            _tenantServiceMock.Object,
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
        result.Status.Should().Be("Pending");
        result.Amount.Should().Be(3500m);

        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Starter); // unchanged -- not yet verified
        _tenantRepoMock.Verify(r => r.Update(It.IsAny<Tenant>()), Times.Never);
        _subscriptionRepoMock.Verify(r => r.AddAsync(
            It.Is<TenantSubscriptionRecord>(s => s.Amount == 3500m && s.PaymentMethod == "bKash" && s.Status == "Pending"),
            It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApprovePendingSubscription_ShouldActivateTenantAndCompleteRecord()
    {
        // Arrange
        var tenant = Tenant.Create("Green Farm", "green-farm", SubscriptionTier.Starter);
        var record = TenantSubscriptionRecord.Create(
            tenantId: tenant.Id,
            tier: SubscriptionTier.Standard,
            billingCycle: SubscriptionBillingCycle.Monthly,
            amount: 3500m,
            currency: "BDT",
            startedAtUtc: DateTime.UtcNow,
            expiresAtUtc: DateTime.UtcNow.AddMonths(1),
            paymentMethod: "bKash",
            paymentReference: "TRX-987654",
            status: "Pending");

        _subscriptionRepoMock.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);
        _tenantRepoMock.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        _tenantRepoMock.Setup(r => r.GetTenantUsageCountsAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((2, 2, 80));

        var handler = new ApprovePendingSubscriptionCommandHandler(
            _subscriptionRepoMock.Object,
            _tenantRepoMock.Object,
            _cacheServiceMock.Object,
            _unitOfWorkMock.Object);

        // Act
        var result = await handler.Handle(new ApprovePendingSubscriptionCommand(record.Id), CancellationToken.None);

        // Assert
        result.Tier.Should().Be("Standard");
        result.Status.Should().Be("Active");
        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Standard);
        record.Status.Should().Be("Completed");

        _tenantRepoMock.Verify(r => r.Update(tenant), Times.Once);
        _subscriptionRepoMock.Verify(r => r.Update(record), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
