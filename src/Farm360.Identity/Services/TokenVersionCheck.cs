using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Security.Claims;

namespace Farm360.Identity.Services;

/// <summary>
/// JwtBearer <c>OnTokenValidated</c> hook: rejects an access token whose <c>tv</c> claim no longer
/// matches the user's TokenVersion (revoked by logout-all / password change / admin action).
/// </summary>
public static class TokenVersionCheck
{
    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var sub = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
        var tv = principal?.FindFirst("tv")?.Value;

        if (!Guid.TryParse(sub, out var userId) ||
            !int.TryParse(tv, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tokenVersion))
        {
            context.Fail("Token is missing the subject or token-version claim.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<ITokenVersionValidator>();
        if (!await validator.IsCurrentAsync(userId, tokenVersion, context.HttpContext.RequestAborted))
        {
            context.Fail("Token has been revoked.");
        }
    }
}
