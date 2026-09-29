using Farm360.Domain.Tenancy;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Tenancy;

/// <summary>
/// Unit tests for Tenant aggregate root.
/// Constitution §17: 70%+ coverage on Domain entities.
/// Pattern: AAA (Arrange, Act, Assert).
/// </summary>
public sealed class TenantTests
{
    // ── Factory tests ─────────────────────────────────────────────────────────
    [Fact]
    public void Create_WithValidInputs_ShouldReturnActiveTenant()
    {
        // Arrange
        var name = "Greenfield Farms";
        var slug = "greenfield-farms";
        var tier = SubscriptionTier.Standard;

        // Act
        var tenant = Tenant.Create(name, slug, tier);

        // Assert
        tenant.Should().NotBeNull();
        tenant.Id.Should().NotBeEmpty();
        tenant.Name.Should().Be(name);
        tenant.Slug.Should().Be(slug);
        tenant.SubscriptionTier.Should().Be(tier);
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ShouldThrow(string? name)
    {
        // Act
        var act = () => Tenant.Create(name!, "slug", SubscriptionTier.Starter);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldNormalizeSlugToLowerCase()
    {
        // Arrange & Act
        var tenant = Tenant.Create("Test Farm", "TEST-FARM", SubscriptionTier.Starter);

        // Assert
        tenant.Slug.Should().Be("test-farm");
    }

    [Fact]
    public void Create_Starter_ShouldSetCorrectQuotas()
    {
        // Act
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Assert
        tenant.MaxUsers.Should().Be(3);
        tenant.MaxFarms.Should().Be(1);
        tenant.MaxAnimals.Should().Be(100);
    }

    [Fact]
    public void Create_Enterprise_ShouldSetUnlimitedQuotas()
    {
        // Act
        var tenant = Tenant.Create("Corp", "corp", SubscriptionTier.Enterprise);

        // Assert
        tenant.MaxUsers.Should().Be(int.MaxValue);
    }

    [Fact]
    public void Create_ShouldRaiseTenantCreatedEvent()
    {
        // Act
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Assert
        tenant.DomainEvents.Should().ContainSingle(e => e is TenantCreatedEvent);
        var evt = (TenantCreatedEvent)tenant.DomainEvents[0];
        evt.TenantId.Should().Be(tenant.Id);
        evt.Slug.Should().Be("farm");
    }

    // ── Status transition tests ───────────────────────────────────────────────
    [Fact]
    public void Suspend_ActiveTenant_ShouldSetSuspendedStatus()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.Suspend();

        // Assert
        tenant.Status.Should().Be(TenantStatus.Suspended);
        tenant.DomainEvents.Should().Contain(e => e is TenantSuspendedEvent);
    }

    [Fact]
    public void Suspend_CancelledTenant_ShouldThrow()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);
        tenant.Cancel();

        // Act
        var act = () => tenant.Suspend();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cancelled*");
    }

    [Fact]
    public void Activate_SuspendedTenant_ShouldClearGracePeriod()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);
        tenant.EnterGracePeriod(DateTime.UtcNow.AddDays(7));

        // Act
        tenant.Activate();

        // Assert
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.GracePeriodEndsAt.Should().BeNull();
    }

    [Fact]
    public void EnterGracePeriod_FromCancelled_ShouldThrow()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);
        tenant.Cancel();

        // Act
        var act = () => tenant.EnterGracePeriod(DateTime.UtcNow.AddDays(7));

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_ShouldSoftDeleteTenant()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.Cancel();

        // Assert
        tenant.Status.Should().Be(TenantStatus.Cancelled);
        tenant.IsDeleted.Should().BeTrue();
        tenant.DeletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void Upgrade_ShouldUpdateTierAndQuotas()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);
        var expiresAt = DateTime.UtcNow.AddYears(1);

        // Act
        tenant.Upgrade(SubscriptionTier.Professional, expiresAt);

        // Assert
        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Professional);
        tenant.SubscriptionExpiresAt.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
        tenant.MaxUsers.Should().Be(50);
        tenant.MaxAnimals.Should().Be(5000);
    }

    [Fact]
    public void UpdateBranding_ShouldUpdateFields()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.UpdateBranding("https://logo.url", "#1A7F4B", "Asia/Dhaka");

        // Assert
        tenant.LogoUrl.Should().Be("https://logo.url");
        tenant.PrimaryColor.Should().Be("#1A7F4B");
        tenant.TimeZone.Should().Be("Asia/Dhaka");
    }

    // ── Trial and Subscription cycle tests ──────────────────────────────────
    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(10)]
    public void StartTrial_WithValidDays_ShouldActivateTrialAndRaiseEvent(int days)
    {
        // Arrange
        var tenant = Tenant.Create("Trial Farm", "trial-farm", SubscriptionTier.Starter);

        // Act
        tenant.StartTrial(days);

        // Assert
        tenant.IsTrial.Should().BeTrue();
        tenant.TrialDays.Should().Be(days);
        tenant.TrialEndsAtUtc.Should().NotBeNull();
        tenant.TrialEndsAtUtc!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(days), TimeSpan.FromSeconds(2));
        tenant.SubscriptionExpiresAt.Should().Be(tenant.TrialEndsAtUtc);
        tenant.BillingCycle.Should().Be(SubscriptionBillingCycle.Trial);
        tenant.HasUsedTrial.Should().BeTrue();
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.DomainEvents.Should().Contain(e => e is TenantTrialStartedEvent);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(30)]
    public void StartTrial_WithInvalidDays_ShouldThrow(int invalidDays)
    {
        // Arrange
        var tenant = Tenant.Create("Trial Farm", "trial-farm", SubscriptionTier.Starter);

        // Act
        var act = () => tenant.StartTrial(invalidDays);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*3, 7, or 10*");
    }

    [Fact]
    public void StartTrial_WhenAlreadyUsedTrial_ShouldThrow()
    {
        // Arrange
        var tenant = Tenant.Create("Trial Farm", "trial-farm", SubscriptionTier.Starter);
        tenant.StartTrial(7);

        // Act
        var act = () => tenant.StartTrial(7);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*already used*");
    }

    [Fact]
    public void Subscribe_Monthly_ShouldCalculateExpirationToOneMonth()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.Subscribe(SubscriptionTier.Standard, SubscriptionBillingCycle.Monthly);

        // Assert
        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Standard);
        tenant.BillingCycle.Should().Be(SubscriptionBillingCycle.Monthly);
        tenant.IsTrial.Should().BeFalse();
        tenant.SubscriptionExpiresAt.Should().NotBeNull();
        tenant.SubscriptionExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddMonths(1), TimeSpan.FromSeconds(3));
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.DomainEvents.Should().Contain(e => e is TenantSubscribedEvent);
    }

    [Fact]
    public void Subscribe_Yearly_ShouldCalculateExpirationToOneYear()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.Subscribe(SubscriptionTier.Professional, SubscriptionBillingCycle.Yearly);

        // Assert
        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Professional);
        tenant.BillingCycle.Should().Be(SubscriptionBillingCycle.Yearly);
        tenant.IsTrial.Should().BeFalse();
        tenant.SubscriptionExpiresAt.Should().NotBeNull();
        tenant.SubscriptionExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddYears(1), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void Subscribe_OneTime_ShouldHaveNoExpiration()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);

        // Act
        tenant.Subscribe(SubscriptionTier.Enterprise, SubscriptionBillingCycle.OneTime);

        // Assert
        tenant.SubscriptionTier.Should().Be(SubscriptionTier.Enterprise);
        tenant.BillingCycle.Should().Be(SubscriptionBillingCycle.OneTime);
        tenant.IsTrial.Should().BeFalse();
        tenant.SubscriptionExpiresAt.Should().BeNull(); // Lifetime access
        tenant.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public void ExtendTrial_ShouldAddDaysToTrial()
    {
        // Arrange
        var tenant = Tenant.Create("Farm", "farm", SubscriptionTier.Starter);
        tenant.StartTrial(7);
        var originalEnd = tenant.TrialEndsAtUtc!.Value;

        // Act
        tenant.ExtendTrial(3);

        // Assert
        tenant.TrialDays.Should().Be(10);
        tenant.TrialEndsAtUtc!.Value.Should().BeCloseTo(originalEnd.AddDays(3), TimeSpan.FromSeconds(2));
    }
}
