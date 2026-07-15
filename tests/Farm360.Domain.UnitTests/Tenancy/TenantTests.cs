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
}
