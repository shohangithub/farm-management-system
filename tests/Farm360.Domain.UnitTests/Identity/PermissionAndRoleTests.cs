using Farm360.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Identity;

/// <summary>
/// Unit tests for Permission and Role domain entities.
/// Constitution §17: Domain invariants must be tested.
/// </summary>
public sealed class PermissionAndRoleTests
{
    // ── Permission tests ──────────────────────────────────────────────────────
    [Fact]
    public void Permission_Create_WithValidCode_ShouldSucceed()
    {
        // Act
        var permission = Permission.Create("animals.view", "Animals", "View animals");

        // Assert
        permission.Code.Should().Be("animals.view");
        permission.Module.Should().Be("Animals");
    }

    [Fact]
    public void Permission_Create_ShouldNormalizeCodeToLowerCase()
    {
        // Act
        var permission = Permission.Create("Animals.VIEW", "Animals", "View animals");

        // Assert
        permission.Code.Should().Be("animals.view");
    }

    [Theory]
    [InlineData("animalsview")]   // missing dot
    [InlineData("")]              // empty
    [InlineData("   ")]           // whitespace
    public void Permission_Create_WithInvalidCode_ShouldThrow(string code)
    {
        // Act
        var act = () => Permission.Create(code, "Animals", "View animals");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // ── Role tests ────────────────────────────────────────────────────────────
    [Fact]
    public void Role_CreateSystemRole_ShouldBeSystemRole()
    {
        // Act
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Owner", "Full access");

        // Assert
        role.IsSystemRole.Should().BeTrue();
        role.TenantId.Should().BeNull();
        role.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Role_CreateTenantRole_ShouldHaveTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();

        // Act
        var role = Role.CreateTenantRole(tenantId, "CustomManager", "Custom role");

        // Assert
        role.IsSystemRole.Should().BeFalse();
        role.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public void Role_UpdateDetails_OnSystemRole_ShouldThrow()
    {
        // Arrange
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Owner", "Full access");

        // Act
        var act = () => role.UpdateDetails("NewName", "New desc");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*System roles*");
    }

    [Fact]
    public void Role_Deactivate_OnSystemRole_ShouldThrow()
    {
        // Arrange
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Owner", "Full access");

        // Act
        var act = () => role.Deactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Role_AddPermission_ShouldAddToCollection()
    {
        // Arrange
        var role = Role.CreateSystemRole(Guid.NewGuid(), "Owner", "Full access");
        var permId = Guid.NewGuid();

        // Act
        role.AddPermission(permId);

        // Assert
        role.RolePermissions.Should().ContainSingle(rp => rp.PermissionId == permId);
    }

    [Fact]
    public void Role_AddPermission_Duplicate_ShouldNotAddTwice()
    {
        // Arrange
        var role = Role.CreateTenantRole(Guid.NewGuid(), "Custom", "Desc");
        var permId = Guid.NewGuid();

        // Act
        role.AddPermission(permId);
        role.AddPermission(permId); // duplicate

        // Assert
        role.RolePermissions.Should().HaveCount(1);
    }

    [Fact]
    public void TenantUser_Invite_ShouldCreatePendingUser()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        // Act
        var tenantUser = TenantUser.Invite(tenantId, userId, roleId);

        // Assert
        tenantUser.Status.Should().Be(TenantUserStatus.Pending);
        tenantUser.InvitedAt.Should().NotBeNull();
        tenantUser.JoinedAt.Should().BeNull();
    }

    [Fact]
    public void TenantUser_Accept_ShouldActivateUser()
    {
        // Arrange
        var tenantUser = TenantUser.Invite(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        tenantUser.Accept();

        // Assert
        tenantUser.Status.Should().Be(TenantUserStatus.Active);
        tenantUser.JoinedAt.Should().NotBeNull();
    }

    [Fact]
    public void TenantUser_CreateOwner_ShouldBeActiveOwner()
    {
        // Act
        var owner = TenantUser.CreateOwner(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Assert
        owner.IsOwner.Should().BeTrue();
        owner.Status.Should().Be(TenantUserStatus.Active);
    }

    [Fact]
    public void TenantUser_Deactivate_Owner_ShouldThrow()
    {
        // Arrange
        var owner = TenantUser.CreateOwner(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => owner.Deactivate();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*owner*");
    }
}
