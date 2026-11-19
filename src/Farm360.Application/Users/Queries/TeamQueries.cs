using Farm360.Application.Common.Interfaces;
using Farm360.Domain.Common;
using Farm360.Domain.Identity.Repositories;
using Farm360.Domain.Organizations.Repositories;
using MediatR;

namespace Farm360.Application.Users.Queries;

public sealed record MemberDto(
    Guid Id,
    Guid UserId,
    string? FullName,
    string? Phone,
    string? Email,
    Guid RoleId,
    string RoleName,
    bool IsOwner,
    Guid? BranchId,
    string? BranchName,
    string Status,
    DateTime? JoinedAt,
    DateTime? LastLoginAt,
    bool IsCurrentUser);

public sealed record InvitationDto(
    Guid Id,
    string? Phone,
    string? Email,
    Guid RoleId,
    string RoleName,
    Guid? BranchId,
    string? BranchName,
    string Status,
    DateTime ExpiresAtUtc,
    DateTime LastSentAtUtc,
    int SendCount,
    string? InvitedByName);

public sealed record RoleOptionDto(Guid Id, string Name, string Description, bool IsSystemRole, bool CanGrant);

// ── Members ──────────────────────────────────────────────────────────────────

public sealed record GetMembersQuery : IRequest<IReadOnlyList<MemberDto>>;

public sealed class GetMembersQueryHandler(
    ITenantService tenantService,
    ICurrentUserService currentUser,
    ITenantUserRepository members,
    IUserAccountService accounts) : IRequestHandler<GetMembersQuery, IReadOnlyList<MemberDto>>
{
    public async Task<IReadOnlyList<MemberDto>> Handle(GetMembersQuery request, CancellationToken cancellationToken)
    {
        var list = await members.GetMembersAsync(tenantService.TenantId, cancellationToken);
        var summaries = await accounts.GetSummariesAsync(list.Select(m => m.UserId).Distinct().ToList(), cancellationToken);

        return list.Select(m =>
        {
            summaries.TryGetValue(m.UserId, out var s);
            return new MemberDto(
                m.Id,
                m.UserId,
                s?.FullName,
                s?.PhoneNumber,
                s?.Email,
                m.RoleId,
                m.Role?.Name ?? "Unknown",
                m.IsOwnerMember,
                m.BranchId,
                m.Branch?.Name,
                m.Status.ToString(),
                m.JoinedAt,
                s?.LastLoginAt,
                m.UserId == currentUser.UserId);
        }).ToList();
    }
}

// ── Invitations ──────────────────────────────────────────────────────────────

public sealed record GetInvitationsQuery : IRequest<IReadOnlyList<InvitationDto>>;

public sealed class GetInvitationsQueryHandler(
    ITenantService tenantService,
    IInvitationRepository invitations,
    IRoleRepository roles,
    IBranchRepository branches,
    IUserAccountService accounts,
    IDateTimeService clock) : IRequestHandler<GetInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    public async Task<IReadOnlyList<InvitationDto>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var now = clock.UtcNow;
        var list = await invitations.ListPendingAsync(tenantId, cancellationToken);
        if (list.Count == 0) return [];

        var roleNames = (await roles.ListAssignableAsync(tenantId, cancellationToken)).ToDictionary(r => r.Id, r => r.Name);
        var branchNames = (await branches.GetLookupsAsync(tenantId, null, cancellationToken)).ToDictionary(b => b.Id, b => b.Name);
        var inviters = await accounts.GetSummariesAsync(list.Select(i => i.InvitedByUserId).Distinct().ToList(), cancellationToken);

        return list.Select(i => new InvitationDto(
            i.Id,
            i.Phone,
            i.Email,
            i.RoleId,
            roleNames.GetValueOrDefault(i.RoleId, "Unknown"),
            i.BranchId,
            i.BranchId is { } b ? branchNames.GetValueOrDefault(b) : null,
            i.EffectiveStatus(now).ToString(),
            i.ExpiresAtUtc,
            i.LastSentAtUtc,
            i.SendCount,
            inviters.TryGetValue(i.InvitedByUserId, out var inviter) ? inviter.FullName ?? inviter.PhoneNumber : null))
            .ToList();
    }
}

// ── Lookups for the Team page ────────────────────────────────────────────────

public sealed record GetAssignableRolesQuery : IRequest<IReadOnlyList<RoleOptionDto>>;

public sealed class GetAssignableRolesQueryHandler(
    ITenantService tenantService,
    ICurrentUserService currentUser,
    IPermissionService permissions,
    IRoleRepository roles) : IRequestHandler<GetAssignableRolesQuery, IReadOnlyList<RoleOptionDto>>
{
    public async Task<IReadOnlyList<RoleOptionDto>> Handle(GetAssignableRolesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = tenantService.TenantId;
        var caller = await MemberAuthority.CallerPermissionsAsync(currentUser, permissions, tenantId, cancellationToken);
        var list = await roles.ListAssignableAsync(tenantId, cancellationToken);

        var result = new List<RoleOptionDto>(list.Count);
        foreach (var role in list)
        {
            var canGrant = await MemberAuthority.CanGrantAsync(roles, caller, role.Id, cancellationToken);
            result.Add(new RoleOptionDto(role.Id, role.Name, role.Description, role.IsSystemRole, canGrant));
        }

        return result;
    }
}

public sealed record GetTeamBranchesQuery : IRequest<IReadOnlyList<LookupItem>>;

public sealed class GetTeamBranchesQueryHandler(
    ITenantService tenantService,
    IBranchRepository branches) : IRequestHandler<GetTeamBranchesQuery, IReadOnlyList<LookupItem>>
{
    public Task<IReadOnlyList<LookupItem>> Handle(GetTeamBranchesQuery request, CancellationToken cancellationToken) =>
        branches.GetLookupsAsync(tenantService.TenantId, null, cancellationToken);
}
