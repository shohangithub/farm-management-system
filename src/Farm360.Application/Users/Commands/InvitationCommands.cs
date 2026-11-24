using System.Globalization;
using System.Net;
using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Common.Phones;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Users.Commands;

/// <summary>
/// Sends the invitation link by SMS and/or email (providers chosen by Sms:Provider / Email:Provider).
/// A failed send throws a readable error, so the surrounding transaction rolls the invitation back.
/// </summary>
public sealed class InvitationSender(
    ISmsService sms,
    IEmailService email,
    IPublicLinkBuilder links)
{
    public async Task SendAsync(
        Invitation invitation,
        string rawToken,
        string tenantName,
        string roleName,
        string? inviterName,
        CancellationToken cancellationToken)
    {
        var link = links.InvitationAcceptLink(rawToken);
        var who = string.IsNullOrWhiteSpace(inviterName) ? "You have been" : $"{inviterName} has";
        var org = string.IsNullOrWhiteSpace(tenantName) ? "an organisation" : tenantName;
        var expires = invitation.ExpiresAtUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

        if (invitation.Phone is { } phone)
        {
            try
            {
                await sms.SendAsync(
                    phone,
                    $"{who} invited you to join {org} on Farm360 as {roleName}. Accept by {expires}: {link}",
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new Farm360.Domain.Exceptions.DomainException(
                    "The invitation SMS could not be sent. Check the phone number or try again in a few minutes.", ex);
            }
        }

        if (invitation.Email is { } address)
        {
            var body =
                $"<p>{WebUtility.HtmlEncode(who)} invited you to join <strong>{WebUtility.HtmlEncode(org)}</strong> " +
                $"on Farm360 as <strong>{WebUtility.HtmlEncode(roleName)}</strong>.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">Accept the invitation</a></p>" +
                $"<p>This link is valid until {expires} (UTC) and can be used once.</p>";
            try
            {
                await email.SendAsync(address, $"You're invited to {org} on Farm360", body, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                throw new Farm360.Domain.Exceptions.DomainException(
                    "The invitation email could not be sent. Check the address or try again in a few minutes.", ex);
            }
        }
    }
}

// ── Create ───────────────────────────────────────────────────────────────────

public sealed record CreateInvitationCommand(string? Phone, string? Email, Guid RoleId, Guid? BranchId)
    : IRequest<Guid>, ITransactionalCommand;

public sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Phone) || !string.IsNullOrWhiteSpace(x.Email))
            .WithName("Phone")
            .WithMessage("Enter a phone number or an email address.");
        RuleFor(x => x.Phone!).Must(PhoneNumberNormalizer.IsValid).WithMessage("Enter a valid phone number (digits only, optional +).")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
        RuleFor(x => x.Email!).EmailAddress().MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class CreateInvitationCommandHandler(
    ITenantService tenantService,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    ITenantRepository tenants,
    ITenantUserRepository members,
    IInvitationRepository invitations,
    IRoleRepository roles,
    IBranchRepository branches,
    IUserAccountService accounts,
    InvitationSender sender,
    IDateTimeService clock,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateInvitationCommand, Guid>
{
    public async Task<Guid> Handle(CreateInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var callerId = currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");
        var now = clock.UtcNow;
        var phone = Contact.NormalizePhone(request.Phone);
        var email = Contact.NormalizeEmail(request.Email);

        var role = await roles.GetAssignableAsync(tenantId, request.RoleId, cancellationToken)
            ?? throw Fail.Validation(nameof(request.RoleId), "Role not found.");
        var caller = await MemberAuthority.CallerPermissionsAsync(currentUser, permissions, tenantId, cancellationToken);
        await MemberAuthority.EnsureCanGrantAsync(roles, caller, role.Id, cancellationToken);

        if (request.BranchId is { } branchId)
        {
            _ = await branches.GetByIdAsync(tenantId, branchId, cancellationToken)
                ?? throw Fail.Validation(nameof(request.BranchId), "Branch not found.");
        }

        // Already a member? (by the account that owns this phone/email)
        foreach (var account in await FindAccountsAsync(phone, email, cancellationToken))
        {
            var existing = await members.GetByUserAsync(tenantId, account.UserId, cancellationToken);
            if (existing is not null)
            {
                throw new ConflictException(existing.Status == TenantUserStatus.Deactivated
                    ? "This person is already a member (deactivated). Reactivate them instead."
                    : "This person is already a member of your organisation.");
            }
        }

        // Seat check takes the tenant lock; the duplicate check below then runs serialised too.
        await SeatQuota.EnsureSeatAvailableAsync(tenants, members, invitations, tenantId, now, null, cancellationToken);

        if (await invitations.HasOpenForAsync(tenantId, phone, email, now, cancellationToken))
            throw new ConflictException("An invitation for this phone number or email is already pending. Resend it instead.");

        var rawToken = SecureTokens.Generate();
        var invitation = Invitation.Create(
            tenantId, phone, email, role.Id, request.BranchId, callerId, SecureTokens.Hash(rawToken), now);
        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var inviter = (await accounts.GetSummariesAsync([callerId], cancellationToken)).GetValueOrDefault(callerId);
        // Sent inside the transaction: if delivery throws, the invitation is rolled back.
        await sender.SendAsync(invitation, rawToken, tenantService.TenantName, role.Name, inviter?.FullName, cancellationToken);

        return invitation.Id;
    }

    private async Task<List<UserAccountSummary>> FindAccountsAsync(string? phone, string? email, CancellationToken ct)
    {
        var found = new List<UserAccountSummary>();
        if (phone is not null && await accounts.FindByPhoneAsync(phone, ct) is { } byPhone) found.Add(byPhone);
        if (email is not null && await accounts.FindByEmailAsync(email, ct) is { } byEmail
            && found.TrueForAll(f => f.UserId != byEmail.UserId)) found.Add(byEmail);
        return found;
    }
}

// ── Resend ───────────────────────────────────────────────────────────────────

public sealed record ResendInvitationCommand(Guid InvitationId) : IRequest, ITransactionalCommand;

public sealed class ResendInvitationCommandHandler(
    ITenantService tenantService,
    ICurrentUserService currentUser,
    ITenantRepository tenants,
    ITenantUserRepository members,
    IInvitationRepository invitations,
    IRoleRepository roles,
    IUserAccountService accounts,
    InvitationSender sender,
    IDateTimeService clock,
    IUnitOfWork unitOfWork) : IRequestHandler<ResendInvitationCommand>
{
    public async Task Handle(ResendInvitationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var now = clock.UtcNow;
        var invitation = await invitations.GetByIdAsync(tenantId, request.InvitationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invitation), request.InvitationId);

        if (invitation.Status != InvitationStatus.Pending)
            throw Fail.Validation(nameof(request.InvitationId), "Only pending invitations can be resent.");

        // An expired invitation no longer holds a seat; resending takes one again.
        if (!invitation.IsUsable(now))
        {
            await SeatQuota.EnsureSeatAvailableAsync(tenants, members, invitations, tenantId, now, invitation.Id, cancellationToken);
        }

        var rawToken = SecureTokens.Generate();
        invitation.Reissue(SecureTokens.Hash(rawToken), now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var role = await roles.GetAssignableAsync(tenantId, invitation.RoleId, cancellationToken);
        var callerId = currentUser.UserId ?? Guid.Empty;
        var inviter = (await accounts.GetSummariesAsync([callerId], cancellationToken)).GetValueOrDefault(callerId);
        await sender.SendAsync(invitation, rawToken, tenantService.TenantName, role?.Name ?? "a member", inviter?.FullName, cancellationToken);
    }
}

// ── Revoke ───────────────────────────────────────────────────────────────────

public sealed record RevokeInvitationCommand(Guid InvitationId) : IRequest, ITransactionalCommand;

public sealed class RevokeInvitationCommandHandler(
    ITenantService tenantService,
    IInvitationRepository invitations,
    IDateTimeService clock,
    IUnitOfWork unitOfWork) : IRequestHandler<RevokeInvitationCommand>
{
    public async Task Handle(RevokeInvitationCommand request, CancellationToken cancellationToken)
    {
        var invitation = await invitations.GetByIdAsync(tenantService.TenantId, request.InvitationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Invitation), request.InvitationId);

        if (invitation.Status != InvitationStatus.Pending)
            throw Fail.Validation(nameof(request.InvitationId), "Only pending invitations can be revoked.");

        invitation.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
