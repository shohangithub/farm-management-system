using System;
using Farm360.Domain.Identity;
using FluentAssertions;
using Xunit;

namespace Farm360.Domain.UnitTests.Identity;

public sealed class InvitationAndMembershipTests
{
    private static readonly DateTime Now = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

    private static Invitation NewInvitation(DateTime? at = null) =>
        Invitation.Create(Guid.NewGuid(), "+8801700000000", "A@Example.com", SystemRoleIds.Worker, null,
            Guid.NewGuid(), "hash-1", at ?? Now);

    // ── Invitation ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_NormalisesContact_AndExpiresInSevenDays()
    {
        var invitation = NewInvitation();

        invitation.Email.Should().Be("a@example.com");
        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.ExpiresAtUtc.Should().Be(Now.AddDays(7));
        invitation.IsUsable(Now).Should().BeTrue();
    }

    [Fact]
    public void Create_WithoutPhoneOrEmail_Throws()
    {
        var act = () => Invitation.Create(Guid.NewGuid(), " ", null, SystemRoleIds.Worker, null, Guid.NewGuid(), "h", Now);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Token_IsSingleUse()
    {
        var invitation = NewInvitation();
        invitation.Accept(Guid.NewGuid(), Now.AddHours(1));

        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.IsUsable(Now.AddHours(2)).Should().BeFalse();

        var second = () => invitation.Accept(Guid.NewGuid(), Now.AddHours(2));
        second.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExpiredInvitation_CannotBeAccepted_AndReadsAsExpired()
    {
        var invitation = NewInvitation();
        var later = Now.AddDays(7).AddSeconds(1);

        invitation.IsUsable(later).Should().BeFalse();
        invitation.EffectiveStatus(later).Should().Be(InvitationStatus.Expired);
        var act = () => invitation.Accept(Guid.NewGuid(), later);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RevokedInvitation_CannotBeAccepted()
    {
        var invitation = NewInvitation();
        invitation.Revoke(Now);

        invitation.IsUsable(Now).Should().BeFalse();
        var act = () => invitation.Accept(Guid.NewGuid(), Now);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Reissue_ReplacesToken_AndRestartsValidity()
    {
        var invitation = NewInvitation();
        var later = Now.AddDays(10); // already expired

        invitation.Reissue("hash-2", later);

        invitation.TokenHash.Should().Be("hash-2");
        invitation.ExpiresAtUtc.Should().Be(later.AddDays(7));
        invitation.SendCount.Should().Be(2);
        invitation.IsUsable(later).Should().BeTrue();
    }

    // ── TenantUser: last-owner guard ─────────────────────────────────────────

    [Fact]
    public void LastOwner_CannotBeDemoted_Deactivated_OrRemoved()
    {
        var owner = TenantUser.CreateOwner(Guid.NewGuid(), Guid.NewGuid(), SystemRoleIds.Owner);

        ((Action)(() => owner.ChangeRole(SystemRoleIds.Worker))).Should().Throw<InvalidOperationException>().WithMessage("*last owner*");
        ((Action)(() => owner.Deactivate())).Should().Throw<InvalidOperationException>().WithMessage("*owner*");
        ((Action)(() => owner.Remove(Guid.NewGuid()))).Should().Throw<InvalidOperationException>().WithMessage("*owner*");
        owner.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public void Owner_CanBeDemoted_WhenAnotherActiveOwnerExists()
    {
        var owner = TenantUser.CreateOwner(Guid.NewGuid(), Guid.NewGuid(), SystemRoleIds.Owner);

        owner.ChangeRole(SystemRoleIds.FarmManager, anotherActiveOwnerExists: true);

        owner.RoleId.Should().Be(SystemRoleIds.FarmManager);
        owner.IsOwner.Should().BeFalse();
        owner.IsOwnerMember.Should().BeFalse();
    }

    [Fact]
    public void Join_WithOwnerRole_IsCoOwner_AndActive()
    {
        var member = TenantUser.Join(Guid.NewGuid(), Guid.NewGuid(), SystemRoleIds.Owner, null);

        member.Status.Should().Be(TenantUserStatus.Active);
        member.IsOwnerMember.Should().BeTrue();
        member.JoinedAt.Should().NotBeNull();
    }

    [Fact]
    public void Member_DeactivateThenReactivate()
    {
        var member = TenantUser.Join(Guid.NewGuid(), Guid.NewGuid(), SystemRoleIds.Worker, Guid.NewGuid());

        member.Deactivate();
        member.Status.Should().Be(TenantUserStatus.Deactivated);
        ((Action)(() => member.Deactivate())).Should().Throw<InvalidOperationException>();

        member.Reactivate();
        member.Status.Should().Be(TenantUserStatus.Active);
    }
}
