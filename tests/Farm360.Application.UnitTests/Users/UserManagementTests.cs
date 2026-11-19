using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Farm360.Application.Auth.Commands;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Users;
using Farm360.Application.Users.Commands;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy;
using Farm360.Domain.Tenancy.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Farm360.Application.UnitTests.Users;

public sealed class UserManagementTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly Tenant _tenant = Tenant.Create("Green Farm", "green-farm", SubscriptionTier.Starter);

    private readonly Mock<ITenantService> _tenantService = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IPermissionService> _permissions = new();
    private readonly Mock<ITenantRepository> _tenants = new();
    private readonly Mock<ITenantUserRepository> _members = new();
    private readonly Mock<IInvitationRepository> _invitations = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IBranchRepository> _branches = new();
    private readonly Mock<IUserAccountService> _accounts = new();
    private readonly Mock<ISmsService> _sms = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IPublicLinkBuilder> _links = new();
    private readonly Mock<IDateTimeService> _clock = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IRefreshTokenService> _refreshTokens = new();
    private readonly Mock<IAccessTokenRevoker> _tokenRevoker = new();

    public UserManagementTests()
    {
        _tenantService.Setup(t => t.TenantId).Returns(_tenantId);
        _tenantService.Setup(t => t.TenantName).Returns("Green Farm");
        _currentUser.Setup(c => c.UserId).Returns(_callerId);
        _currentUser.Setup(c => c.TenantId).Returns(_tenantId);
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _tenants.Setup(t => t.GetByIdAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(_tenant);
        _links.Setup(l => l.InvitationAcceptLink(It.IsAny<string>())).Returns("http://app/auth/accept-invitation?token=x");
        _accounts.Setup(a => a.GetSummariesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserAccountSummary>());

        // Caller is an owner with every permission; every role grants "users.view" only.
        _permissions.Setup(p => p.GetPermissionsAsync(_callerId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["users.view", "users.invite", "users.edit", "users.remove", "users.assignrole", "billing.manage"]);
        _roles.Setup(r => r.GetPermissionCodesAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(["users.view"]);
        _roles.Setup(r => r.GetAssignableAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, Guid id, CancellationToken _) => Role.CreateSystemRole(id, "Worker", "Worker"));
    }

    // ── Plan limit: MaxUsers counts members AND pending invitations ──────────

    private CreateInvitationCommandHandler CreateInvitationHandler() => new(
        _tenantService.Object, _currentUser.Object, _permissions.Object, _tenants.Object, _members.Object,
        _invitations.Object, _roles.Object, _branches.Object, _accounts.Object,
        new InvitationSender(_sms.Object, _email.Object, _links.Object), _clock.Object, _unitOfWork.Object);

    private void SetSeats(int members, int openInvitations)
    {
        _members.Setup(m => m.CountSeatHoldersAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(members);
        _invitations.Setup(i => i.CountOpenAsync(_tenantId, Now, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(openInvitations);
    }

    [Fact]
    public async Task Invite_WhenMembersPlusPendingInvitesFillThePlan_IsRejected()
    {
        SetSeats(members: _tenant.MaxUsers - 1, openInvitations: 1);

        var act = () => CreateInvitationHandler().Handle(
            new CreateInvitationCommand("+8801711111111", null, SystemRoleIds.Worker, null), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Users");
        _invitations.Verify(i => i.Add(It.IsAny<Invitation>()), Times.Never);
        _tenants.Verify(t => t.AcquireQuotaLockAsync(_tenantId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task Invite_WithFreeSeat_StoresHashedToken_AndSendsSms()
    {
        SetSeats(members: _tenant.MaxUsers - 2, openInvitations: 1);
        Invitation? stored = null;
        _invitations.Setup(i => i.Add(It.IsAny<Invitation>())).Callback<Invitation>(i => stored = i);

        await CreateInvitationHandler().Handle(
            new CreateInvitationCommand("+8801711111111", null, SystemRoleIds.Worker, null), CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.TokenHash.Should().HaveLength(64);
        stored.ExpiresAtUtc.Should().Be(Now.AddDays(7));
        _sms.Verify(s => s.SendAsync("+8801711111111", It.Is<string>(m => m.Contains("accept-invitation")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invite_RoleWithPermissionsCallerLacks_IsForbidden()
    {
        SetSeats(0, 0);
        _roles.Setup(r => r.GetPermissionCodesAsync(SystemRoleIds.Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["users.view", "finance.admin"]);

        var act = () => CreateInvitationHandler().Handle(
            new CreateInvitationCommand("+8801711111111", null, SystemRoleIds.Owner, null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    // ── Accept: single use, expiry, existing vs new user, quota re-check ─────

    private AcceptInvitationCommandHandler AcceptHandler() => new(
        _invitations.Object, _tenants.Object, _members.Object, _roles.Object, _accounts.Object, _clock.Object, _unitOfWork.Object);

    private Invitation ArrangeInvitation(string rawToken, DateTime createdAt, string? phone = "+8801722222222")
    {
        var invitation = Invitation.Create(_tenantId, phone, phone is null ? "new@example.com" : null,
            SystemRoleIds.Worker, null, _callerId, SecureTokens.Hash(rawToken), createdAt);
        _invitations.Setup(i => i.FindTenantIdByTokenHashAsync(SecureTokens.Hash(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tenantId);
        _invitations.Setup(i => i.GetByTokenHashAsync(SecureTokens.Hash(rawToken), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        return invitation;
    }

    [Fact]
    public async Task Accept_ExistingUser_JoinsWithoutCreatingAccount()
    {
        var invitation = ArrangeInvitation("tok-existing", Now.AddDays(-1));
        var existingUserId = Guid.NewGuid();
        _accounts.Setup(a => a.FindByPhoneAsync("+8801722222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccountSummary(existingUserId, "Rahim", "+8801722222222", null, null));
        SetSeats(1, 1);

        var result = await AcceptHandler().Handle(new AcceptInvitationCommand("tok-existing", null, "MyOld!Pass1", null), CancellationToken.None);

        result.AccountCreated.Should().BeFalse();
        _accounts.Verify(a => a.VerifyPasswordAsync(existingUserId, "MyOld!Pass1", "Password", It.IsAny<CancellationToken>()), Times.Once);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedByUserId.Should().Be(existingUserId);
        _accounts.Verify(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _members.Verify(m => m.AddAsync(It.Is<TenantUser>(t => t.UserId == existingUserId && t.Status == TenantUserStatus.Active), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_ExistingUser_WithoutPassword_IsRefusedAndNothingChanges()
    {
        var invitation = ArrangeInvitation("tok-existing-nopwd", Now.AddDays(-1));
        _accounts.Setup(a => a.FindByPhoneAsync("+8801722222222", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccountSummary(Guid.NewGuid(), "Rahim", "+8801722222222", null, null));
        SetSeats(1, 1);

        var act = () => AcceptHandler().Handle(new AcceptInvitationCommand("tok-existing-nopwd", null, null, null), CancellationToken.None);

        await act.Should().ThrowAsync<Farm360.Application.Common.Exceptions.ValidationException>();
        invitation.Status.Should().NotBe(InvitationStatus.Accepted);
        _members.Verify(m => m.AddAsync(It.IsAny<TenantUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Accept_NewUser_CreatesAccountThenJoins()
    {
        var invitation = ArrangeInvitation("tok-new", Now.AddDays(-1));
        var newUserId = Guid.NewGuid();
        _accounts.Setup(a => a.CreateUserAsync("+8801722222222", null, "Karim", "Str0ng!Pass", It.IsAny<CancellationToken>()))
            .ReturnsAsync(newUserId);
        SetSeats(1, 1);

        var result = await AcceptHandler().Handle(
            new AcceptInvitationCommand("tok-new", "Karim", "Str0ng!Pass", null), CancellationToken.None);

        result.AccountCreated.Should().BeTrue();
        invitation.AcceptedByUserId.Should().Be(newUserId);
        _members.Verify(m => m.AddAsync(It.Is<TenantUser>(t => t.UserId == newUserId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Accept_NewUser_WithoutPassword_IsRejected()
    {
        ArrangeInvitation("tok-nopwd", Now.AddDays(-1));
        SetSeats(1, 1);

        var act = () => AcceptHandler().Handle(new AcceptInvitationCommand("tok-nopwd", "Karim", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Accept_TokenIsSingleUse()
    {
        var invitation = ArrangeInvitation("tok-once", Now.AddDays(-1));
        invitation.Accept(Guid.NewGuid(), Now.AddMinutes(-5));

        var act = () => AcceptHandler().Handle(new AcceptInvitationCommand("tok-once", "X", "Str0ng!Pass", null), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Token");
    }

    [Fact]
    public async Task Accept_ExpiredToken_IsRejected()
    {
        ArrangeInvitation("tok-old", Now.AddDays(-8));

        var act = () => AcceptHandler().Handle(new AcceptInvitationCommand("tok-old", "X", "Str0ng!Pass", null), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Token");
    }

    [Fact]
    public async Task Accept_RechecksMaxUsers_InsideTheTransaction()
    {
        ArrangeInvitation("tok-full", Now.AddDays(-1));
        // Plan filled by members alone (this invitation is excluded from the count while accepting).
        _members.Setup(m => m.CountSeatHoldersAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(_tenant.MaxUsers);
        _invitations.Setup(i => i.CountOpenAsync(_tenantId, Now, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var act = () => AcceptHandler().Handle(new AcceptInvitationCommand("tok-full", "X", "Str0ng!Pass", null), CancellationToken.None);

        (await act.Should().ThrowAsync<ValidationException>()).Which.Errors.Should().ContainKey("Users");
        _tenants.Verify(t => t.AcquireQuotaLockAsync(_tenantId, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        _accounts.Verify(a => a.CreateUserAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Members: last owner, self, deactivation revokes sessions ─────────────

    private MemberCommandContext Ctx() => new(
        _tenantService.Object, _currentUser.Object, _permissions.Object, _members.Object, _roles.Object, _tenants.Object);

    private TenantUser ArrangeMember(TenantUser member, bool anotherOwner)
    {
        _members.Setup(m => m.GetMemberAsync(_tenantId, member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(member);
        _members.Setup(m => m.AnotherActiveOwnerExistsAsync(_tenantId, member.Id, It.IsAny<CancellationToken>())).ReturnsAsync(anotherOwner);
        return member;
    }

    [Fact]
    public async Task Remove_LastOwner_IsRejected()
    {
        var owner = ArrangeMember(TenantUser.CreateOwner(_tenantId, Guid.NewGuid(), SystemRoleIds.Owner), anotherOwner: false);

        var act = () => new RemoveMemberCommandHandler(Ctx(), _refreshTokens.Object, _unitOfWork.Object)
            .Handle(new RemoveMemberCommand(owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last tenant owner*");
        owner.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Demote_LastOwner_IsRejected()
    {
        var owner = ArrangeMember(TenantUser.CreateOwner(_tenantId, Guid.NewGuid(), SystemRoleIds.Owner), anotherOwner: false);

        var act = () => new ChangeMemberRoleCommandHandler(Ctx(), _tokenRevoker.Object, _unitOfWork.Object)
            .Handle(new ChangeMemberRoleCommand(owner.Id, SystemRoleIds.Worker), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*last owner*");
    }

    [Fact]
    public async Task Deactivate_Self_IsRejected()
    {
        var self = ArrangeMember(TenantUser.Join(_tenantId, _callerId, SystemRoleIds.FarmManager, null), anotherOwner: true);

        var act = () => new DeactivateMemberCommandHandler(Ctx(), _refreshTokens.Object, _unitOfWork.Object)
            .Handle(new DeactivateMemberCommand(self.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Deactivate_RevokesAllSessionsOfTheMember()
    {
        var memberUserId = Guid.NewGuid();
        var member = ArrangeMember(TenantUser.Join(_tenantId, memberUserId, SystemRoleIds.Worker, null), anotherOwner: true);

        await new DeactivateMemberCommandHandler(Ctx(), _refreshTokens.Object, _unitOfWork.Object)
            .Handle(new DeactivateMemberCommand(member.Id), CancellationToken.None);

        member.Status.Should().Be(TenantUserStatus.Deactivated);
        _refreshTokens.Verify(r => r.RevokeAllSessionsAsync(memberUserId, SessionRevokeReason.AdminRevoke, It.IsAny<CancellationToken>()), Times.Once);
        _permissions.Verify(p => p.InvalidatePermissionCacheAsync(memberUserId, _tenantId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Manager_CannotManageAMemberWithMorePermissions()
    {
        _permissions.Setup(p => p.GetPermissionsAsync(_callerId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["users.view", "users.edit"]);
        _roles.Setup(r => r.GetPermissionCodesAsync(SystemRoleIds.Owner, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["users.view", "users.edit", "billing.manage"]);
        var owner = ArrangeMember(TenantUser.CreateOwner(_tenantId, Guid.NewGuid(), SystemRoleIds.Owner), anotherOwner: true);

        var act = () => new DeactivateMemberCommandHandler(Ctx(), _refreshTokens.Object, _unitOfWork.Object)
            .Handle(new DeactivateMemberCommand(owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>();
        _refreshTokens.Verify(r => r.RevokeAllSessionsAsync(It.IsAny<Guid>(), It.IsAny<SessionRevokeReason>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Password reset: no user enumeration ──────────────────────────────────

    private readonly Mock<IOtpService> _otp = new();
    private readonly Mock<ICacheService> _cache = new();

    [Fact]
    public async Task ForgotPassword_UnknownPhone_CompletesLikeAKnownOne_WithoutSendingAnything()
    {
        _accounts.Setup(a => a.FindByPhoneAsync("+8800000000000", It.IsAny<CancellationToken>())).ReturnsAsync((UserAccountSummary?)null);
        var handler = new RequestPasswordResetCommandHandler(_accounts.Object, _otp.Object, NullLogger<RequestPasswordResetCommandHandler>.Instance, TimeSpan.Zero);

        var act = () => handler.Handle(new RequestPasswordResetCommand("+8800000000000"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _otp.Verify(o => o.GenerateAndSendAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPassword_KnownPhone_SendsResetOtp()
    {
        _accounts.Setup(a => a.FindByPhoneAsync("+8801733333333", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccountSummary(Guid.NewGuid(), null, "+8801733333333", null, null));
        var handler = new RequestPasswordResetCommandHandler(_accounts.Object, _otp.Object, NullLogger<RequestPasswordResetCommandHandler>.Instance, TimeSpan.Zero);

        await handler.Handle(new RequestPasswordResetCommand("+8801733333333"), CancellationToken.None);

        _otp.Verify(o => o.GenerateAndSendAsync("+8801733333333", OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyCode_UnknownPhone_FailsWithTheSameMessageAsAWrongCode()
    {
        _otp.Setup(o => o.VerifyAsync(It.IsAny<string>(), It.IsAny<string>(), OtpPurpose.PasswordReset, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = new VerifyPasswordResetCodeCommandHandler(_accounts.Object, _otp.Object, _cache.Object);

        var unknown = await FluentActions.Awaiting(() => handler.Handle(new VerifyPasswordResetCodeCommand("+8800000000000", "123456"), CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        _accounts.Setup(a => a.FindByPhoneAsync("+8801733333333", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccountSummary(Guid.NewGuid(), null, "+8801733333333", null, null));
        var wrongCode = await FluentActions.Awaiting(() => handler.Handle(new VerifyPasswordResetCodeCommand("+8801733333333", "654321"), CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();

        unknown.Which.Errors.Should().BeEquivalentTo(wrongCode.Which.Errors);
    }

    [Fact]
    public async Task ResetPassword_TokenIsSingleUse_AndRevokesSessions()
    {
        var userId = Guid.NewGuid();
        var key = PasswordReset.CacheKey("reset-token");
        _cache.Setup(c => c.GetAsync<string>(key, It.IsAny<CancellationToken>())).ReturnsAsync(userId.ToString());
        var handler = new ResetPasswordCommandHandler(_cache.Object, _accounts.Object, _refreshTokens.Object);

        await handler.Handle(new ResetPasswordCommand("reset-token", "N3w!Password"), CancellationToken.None);

        _cache.Verify(c => c.RemoveAsync(key, It.IsAny<CancellationToken>()), Times.Once);
        _accounts.Verify(a => a.ResetPasswordAsync(userId, "N3w!Password", It.IsAny<CancellationToken>()), Times.Once);
        _refreshTokens.Verify(r => r.RevokeAllSessionsAsync(userId, SessionRevokeReason.PasswordChange, It.IsAny<CancellationToken>()), Times.Once);
    }
}
