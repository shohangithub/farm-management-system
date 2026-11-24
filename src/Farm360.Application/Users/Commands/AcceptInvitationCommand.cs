using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Phones;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Users.Commands;

internal static class InvitationLinks
{
    public const string InvalidMessage = "This invitation link is invalid or has expired. Ask for a new invitation.";

    /// <summary>The account the invitation addresses: phone match first, then email.</summary>
    public static async Task<UserAccountSummary?> FindInviteeAsync(
        IUserAccountService accounts, Invitation invitation, CancellationToken cancellationToken)
    {
        if (invitation.Phone is { } phone && await accounts.FindByPhoneAsync(phone, cancellationToken) is { } byPhone)
            return byPhone;
        if (invitation.Email is { } email && await accounts.FindByEmailAsync(email, cancellationToken) is { } byEmail)
            return byEmail;
        return null;
    }
}

// ── Preview (anonymous) ──────────────────────────────────────────────────────

public sealed record InvitationPreviewDto(
    string TenantName,
    string RoleName,
    string? PhoneMasked,
    string? EmailMasked,
    DateTime ExpiresAtUtc,
    bool HasAccount,
    bool NeedsPhone);

public sealed record GetInvitationPreviewQuery(string Token) : IRequest<InvitationPreviewDto>;

public sealed class GetInvitationPreviewQueryHandler(
    IInvitationRepository invitations,
    ITenantRepository tenants,
    IRoleRepository roles,
    IUserAccountService accounts,
    IDateTimeService clock) : IRequestHandler<GetInvitationPreviewQuery, InvitationPreviewDto>
{
    public async Task<InvitationPreviewDto> Handle(GetInvitationPreviewQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token)) throw Fail.Validation("Token", InvitationLinks.InvalidMessage);

        var invitation = await invitations.GetByTokenHashAsync(SecureTokens.Hash(request.Token.Trim()), cancellationToken);
        if (invitation is null || !invitation.IsUsable(clock.UtcNow))
            throw Fail.Validation("Token", InvitationLinks.InvalidMessage);

        var tenant = await tenants.GetByIdAsync(invitation.TenantId, cancellationToken);
        var role = await roles.GetAssignableAsync(invitation.TenantId, invitation.RoleId, cancellationToken);
        var invitee = await InvitationLinks.FindInviteeAsync(accounts, invitation, cancellationToken);

        return new InvitationPreviewDto(
            tenant?.Name ?? "Farm360",
            role?.Name ?? "Member",
            Contact.MaskPhone(invitation.Phone),
            Contact.MaskEmail(invitation.Email),
            invitation.ExpiresAtUtc,
            HasAccount: invitee is not null,
            NeedsPhone: invitee is null && invitation.Phone is null);
    }
}

// ── Accept (anonymous) ───────────────────────────────────────────────────────

public sealed record AcceptInvitationResult(string TenantName, bool AccountCreated, bool AlreadyMember);

/// <summary>
/// Accepts an invitation. An existing account (same phone, else same email) joins the tenant only
/// after confirming its password, so a forwarded or intercepted link cannot add someone else's
/// account to an organisation. A new person sets name + password
/// (and a phone when the invitation was email-only) and an account is created. Single-use: the
/// invitation is marked Accepted in the same transaction, and its row version rejects a racing accept.
/// </summary>
public sealed record AcceptInvitationCommand(string Token, string? FullName, string? Password, string? Phone)
    : IRequest<AcceptInvitationResult>, ITransactionalCommand;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullName!).MaximumLength(150).When(x => x.FullName is not null);
        // The policy applies to a NEW password (new-account flow sends a name); an existing account
        // confirms its current password, which may predate the current policy.
        RuleFor(x => x.Password!).MeetsPasswordPolicy()
            .When(x => !string.IsNullOrEmpty(x.Password) && !string.IsNullOrWhiteSpace(x.FullName));
        RuleFor(x => x.Phone!).Must(PhoneNumberNormalizer.IsValid).WithMessage("Enter a valid phone number.")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public sealed class AcceptInvitationCommandHandler(
    IInvitationRepository invitations,
    ITenantRepository tenants,
    ITenantUserRepository members,
    IRoleRepository roles,
    IUserAccountService accounts,
    IDateTimeService clock,
    IUnitOfWork unitOfWork) : IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>
{
    public async Task<AcceptInvitationResult> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var tokenHash = SecureTokens.Hash(request.Token.Trim());

        // Lock the tenant before loading the invitation, so a concurrent accept/resend/revoke of the
        // same invitation (or a racing seat check) is serialised and we read its latest state.
        var tenantId = await invitations.FindTenantIdByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw Fail.Validation("Token", InvitationLinks.InvalidMessage);
        await tenants.AcquireQuotaLockAsync(tenantId, cancellationToken);

        var invitation = await invitations.GetByTokenHashAsync(tokenHash, cancellationToken);
        if (invitation is null || !invitation.IsUsable(now))
            throw Fail.Validation("Token", InvitationLinks.InvalidMessage);

        var tenant = await tenants.GetByIdAsync(tenantId, cancellationToken)
            ?? throw Fail.Validation("Token", InvitationLinks.InvalidMessage);

        var invitee = await InvitationLinks.FindInviteeAsync(accounts, invitation, cancellationToken);
        var membership = invitee is null ? null : await members.GetByUserAsync(tenantId, invitee.UserId, cancellationToken);

        if (invitee is not null && membership?.Status == TenantUserStatus.Active)
        {
            invitation.Accept(invitee.UserId, now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new AcceptInvitationResult(tenant.Name, AccountCreated: false, AlreadyMember: true);
        }

        // Re-check the plan limit inside the transaction (this invitation already holds a seat).
        await SeatQuota.EnsureSeatAvailableAsync(tenants, members, invitations, tenantId, now, invitation.Id, cancellationToken);

        var role = await roles.GetAssignableAsync(tenantId, invitation.RoleId, cancellationToken)
            ?? throw Fail.Validation("Token", "The role on this invitation no longer exists. Ask for a new invitation.");

        Guid userId;
        var created = false;
        if (invitee is not null)
        {
            if (string.IsNullOrEmpty(request.Password))
                throw Fail.Validation(nameof(request.Password), "Enter your Farm360 password to join this organisation.");
            await accounts.VerifyPasswordAsync(invitee.UserId, request.Password, nameof(request.Password), cancellationToken);
            userId = invitee.UserId;
        }
        else
        {
            var phone = invitation.Phone ?? Contact.NormalizePhone(request.Phone)
                ?? throw Fail.Validation(nameof(request.Phone), "Enter your mobile number — it will be your sign-in ID.");
            if (string.IsNullOrWhiteSpace(request.FullName))
                throw Fail.Validation(nameof(request.FullName), "Enter your name.");
            if (string.IsNullOrEmpty(request.Password))
                throw Fail.Validation(nameof(request.Password), "Choose a password.");
            if (invitation.Phone is null && await accounts.FindByPhoneAsync(phone, cancellationToken) is not null)
            {
                throw Fail.Validation(nameof(request.Phone),
                    "An account already uses this phone number. Ask the inviter to invite that phone number instead.");
            }

            userId = await accounts.CreateUserAsync(phone, invitation.Email, request.FullName.Trim(), request.Password, cancellationToken);
            created = true;
        }

        if (membership is not null)
        {
            // Previously deactivated in this tenant: the invitation brings them back with its role/branch.
            membership.Reactivate();
            membership.ChangeRole(role.Id, anotherActiveOwnerExists: true);
            membership.AssignBranch(invitation.BranchId);
        }
        else
        {
            await members.AddAsync(TenantUser.Join(tenantId, userId, role.Id, invitation.BranchId), cancellationToken);
        }

        invitation.Accept(userId, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AcceptInvitationResult(tenant.Name, created, AlreadyMember: false);
    }
}
