using Farm360.Application.Demo;
using Farm360.Identity.Entities;
using Microsoft.AspNetCore.Identity;

namespace Farm360.Identity.Services;

/// <summary>
/// Accounts behind the public demo: the shared read-only Viewer (fixed id) and the throwaway Owner
/// each build runs as. Both get a random password that is never stored or shown, so neither can
/// be signed in to with a password; the Viewer is only reachable through the demo login.
/// </summary>
public sealed class DemoAccountService(UserManager<ApplicationUser> userManager) : IDemoAccountService
{
    public async Task EnsureViewerUserAsync(CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByIdAsync(DemoConstants.ViewerUserId.ToString()) is not null)
            return;

        var viewer = new ApplicationUser
        {
            Id = DemoConstants.ViewerUserId,
            UserName = DemoConstants.ViewerUserName,
            IsSystemUser = false,
        };

        await CreateAsync(viewer, DemoConstants.ViewerFullName);
    }

    public async Task<Guid> CreateOwnerUserAsync(string userName, string fullName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var owner = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsSystemUser = false,
        };

        await CreateAsync(owner, fullName);
        return owner.Id;
    }

    public async Task<bool> IsSafeViewerAsync(CancellationToken cancellationToken = default)
    {
        var viewer = await userManager.FindByIdAsync(DemoConstants.ViewerUserId.ToString());
        return viewer is not null
               && !viewer.IsSystemUser
               && string.Equals(viewer.UserName, DemoConstants.ViewerUserName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateAsync(ApplicationUser user, string fullName)
    {
        // A random GUID (plus one character of each class the password policy asks for). Not kept anywhere.
        var password = $"{Guid.NewGuid():N}Aa1!";
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create a demo account: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            await UserProfileService.SetFullNameAsync(userManager, user, fullName);
        }
    }
}
