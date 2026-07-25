using Farm360.Application.Common.Interfaces;
using MediatR;

namespace Farm360.Application.Auth.Queries;

public record UserProfileDto(
    Guid Id,
    Guid TenantId,
    string Role,
    string Tier,
    bool IsSystemUser,
    IReadOnlyList<string> Permissions);

public record GetCurrentUserQuery() : IRequest<UserProfileDto>;

internal sealed class GetCurrentUserQueryHandler(
    ICurrentUserService currentUserService,
    IPermissionService permissionService) : IRequestHandler<GetCurrentUserQuery, UserProfileDto>
{
    public async Task<UserProfileDto> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var userId = currentUserService.UserId.Value;
        var tenantId = currentUserService.TenantId ?? Guid.Empty;

        var permissions = await permissionService.GetPermissionsAsync(userId, tenantId, cancellationToken);

        return new UserProfileDto(
            Id: userId,
            TenantId: tenantId,
            Role: currentUserService.Role ?? "Viewer",
            Tier: currentUserService.SubscriptionTier ?? "Starter",
            IsSystemUser: currentUserService.IsSystemUser,
            Permissions: permissions
        );
    }
}
