using Farm360.Application.Common.Behaviors;
using Farm360.Application.Common.Exceptions;
using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Identity;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Organizations.Repositories;
using Farm360.Domain.Tenancy.Repositories;
using FluentValidation;
using MediatR;

namespace Farm360.Application.Users.Commands;

/// <summary>Shared lookups and guards for the member-management commands.</summary>
public sealed class MemberCommandContext(
    ITenantService tenantService,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    ITenantUserRepository members,
    IRoleRepository roles,
    ITenantRepository tenants)
{
    public Guid TenantId => tenantService.TenantId;

    public Guid RequireCallerId() => currentUser.UserId ?? throw new UnauthorizedAccessException("User is not authenticated.");

    public ITenantUserRepository Members => members;
    public IRoleRepository Roles => roles;
    public ITenantRepository Tenants => tenants;

    /// <summary>
    /// Loads the member, refuses self-service actions that could lock the caller out, and refuses
    /// to act on a member whose role holds permissions the caller does not have.
    /// </summary>
    public async Task<(TenantUser Member, HashSet<string>? CallerPermissions)> LoadManageableAsync(
        Guid memberId, string selfActionMessage, CancellationToken cancellationToken)
    {
        var member = await members.GetMemberAsync(TenantId, memberId, cancellationToken)
            ?? throw new NotFoundException(nameof(TenantUser), memberId);

        if (member.UserId == RequireCallerId())
            throw Fail.Validation("MemberId", selfActionMessage);

        var caller = await MemberAuthority.CallerPermissionsAsync(currentUser, permissions, TenantId, cancellationToken);
        await MemberAuthority.EnsureCanManageAsync(roles, caller, member, cancellationToken);
        return (member, caller);
    }

    /// <summary>Serialises owner-affecting changes per tenant, then checks for another active owner.</summary>
    public async Task<bool> AnotherActiveOwnerExistsAsync(TenantUser member, CancellationToken cancellationToken)
    {
        await tenants.AcquireQuotaLockAsync(TenantId, cancellationToken);
        return await members.AnotherActiveOwnerExistsAsync(TenantId, member.Id, cancellationToken);
    }

    public Task InvalidatePermissionsAsync(TenantUser member, CancellationToken cancellationToken) =>
        permissions.InvalidatePermissionCacheAsync(member.UserId, TenantId, cancellationToken);
}

// ── Change role ──────────────────────────────────────────────────────────────

public sealed record ChangeMemberRoleCommand(Guid MemberId, Guid RoleId) : IRequest, ITransactionalCommand;

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class ChangeMemberRoleCommandHandler(
    MemberCommandContext ctx,
    IAccessTokenRevoker tokenRevoker,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeMemberRoleCommand>
{
    public async Task Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var (member, caller) = await ctx.LoadManageableAsync(
            request.MemberId, "You cannot change your own role.", cancellationToken);

        _ = await ctx.Roles.GetAssignableAsync(ctx.TenantId, request.RoleId, cancellationToken)
            ?? throw Fail.Validation(nameof(request.RoleId), "Role not found.");
        await MemberAuthority.EnsureCanGrantAsync(ctx.Roles, caller, request.RoleId, cancellationToken);

        if (member.RoleId == request.RoleId) return;

        var anotherOwner = await ctx.AnotherActiveOwnerExistsAsync(member, cancellationToken);
        member.ChangeRole(request.RoleId, anotherOwner);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ctx.InvalidatePermissionsAsync(member, cancellationToken);
        // New role must apply now, not when the current access token expires.
        await tokenRevoker.InvalidateAccessTokensAsync(member.UserId, cancellationToken);
    }
}

// ── Change branch ────────────────────────────────────────────────────────────

public sealed record ChangeMemberBranchCommand(Guid MemberId, Guid? BranchId) : IRequest, ITransactionalCommand;

public sealed class ChangeMemberBranchCommandValidator : AbstractValidator<ChangeMemberBranchCommand>
{
    public ChangeMemberBranchCommandValidator()
    {
        RuleFor(x => x.MemberId).NotEmpty();
    }
}

public sealed class ChangeMemberBranchCommandHandler(
    MemberCommandContext ctx,
    IBranchRepository branches,
    IAccessTokenRevoker tokenRevoker,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeMemberBranchCommand>
{
    public async Task Handle(ChangeMemberBranchCommand request, CancellationToken cancellationToken)
    {
        var (member, _) = await ctx.LoadManageableAsync(
            request.MemberId, "You cannot change your own branch assignment.", cancellationToken);

        if (request.BranchId is { } branchId)
        {
            _ = await branches.GetByIdAsync(ctx.TenantId, branchId, cancellationToken)
                ?? throw Fail.Validation(nameof(request.BranchId), "Branch not found.");
        }

        if (member.BranchId == request.BranchId) return;

        member.AssignBranch(request.BranchId);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // The farms claim is derived from the branch; reissue tokens so it takes effect now.
        await tokenRevoker.InvalidateAccessTokensAsync(member.UserId, cancellationToken);
    }
}

// ── Deactivate / reactivate ──────────────────────────────────────────────────

public sealed record DeactivateMemberCommand(Guid MemberId) : IRequest, ITransactionalCommand;

public sealed class DeactivateMemberCommandHandler(
    MemberCommandContext ctx,
    IRefreshTokenService refreshTokens,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateMemberCommand>
{
    public async Task Handle(DeactivateMemberCommand request, CancellationToken cancellationToken)
    {
        var (member, _) = await ctx.LoadManageableAsync(
            request.MemberId, "You cannot deactivate yourself.", cancellationToken);

        var anotherOwner = await ctx.AnotherActiveOwnerExistsAsync(member, cancellationToken);
        member.Deactivate(anotherOwner);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ctx.InvalidatePermissionsAsync(member, cancellationToken);
        // Signs the member out everywhere: refresh tokens revoked + token version bumped.
        await refreshTokens.RevokeAllSessionsAsync(member.UserId, SessionRevokeReason.AdminRevoke, cancellationToken);
    }
}

public sealed record ReactivateMemberCommand(Guid MemberId) : IRequest, ITransactionalCommand;

public sealed class ReactivateMemberCommandHandler(
    MemberCommandContext ctx,
    IInvitationRepository invitations,
    IDateTimeService clock,
    IUnitOfWork unitOfWork) : IRequestHandler<ReactivateMemberCommand>
{
    public async Task Handle(ReactivateMemberCommand request, CancellationToken cancellationToken)
    {
        var (member, _) = await ctx.LoadManageableAsync(
            request.MemberId, "You cannot reactivate yourself.", cancellationToken);

        if (member.Status != TenantUserStatus.Deactivated)
            throw Fail.Validation(nameof(request.MemberId), "Only deactivated members can be reactivated.");

        // A reactivated member takes a seat again.
        await SeatQuota.EnsureSeatAvailableAsync(
            ctx.Tenants, ctx.Members, invitations, ctx.TenantId, clock.UtcNow, null, cancellationToken);

        member.Reactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ctx.InvalidatePermissionsAsync(member, cancellationToken);
    }
}

// ── Remove ───────────────────────────────────────────────────────────────────

public sealed record RemoveMemberCommand(Guid MemberId) : IRequest, ITransactionalCommand;

public sealed class RemoveMemberCommandHandler(
    MemberCommandContext ctx,
    IRefreshTokenService refreshTokens,
    IUnitOfWork unitOfWork) : IRequestHandler<RemoveMemberCommand>
{
    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var (member, _) = await ctx.LoadManageableAsync(
            request.MemberId, "You cannot remove yourself from the organisation.", cancellationToken);

        var anotherOwner = await ctx.AnotherActiveOwnerExistsAsync(member, cancellationToken);
        member.Remove(ctx.RequireCallerId(), anotherOwner);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ctx.InvalidatePermissionsAsync(member, cancellationToken);
        await refreshTokens.RevokeAllSessionsAsync(member.UserId, SessionRevokeReason.AdminRevoke, cancellationToken);
    }
}
