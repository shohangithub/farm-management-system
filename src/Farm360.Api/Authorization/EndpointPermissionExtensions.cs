using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Farm360.Api.Authorization;

/// <summary>
/// Helpers for attaching permission policies to Minimal API endpoints and route groups.
/// </summary>
public static class EndpointPermissionExtensions
{
    public const string PermissionPolicyPrefix = "Permission:";

    /// <summary>The policy name the <see cref="PermissionPolicyProvider"/> resolves for a permission code.</summary>
    public static string Policy(string permissionCode) => PermissionPolicyPrefix + permissionCode;

    /// <summary>Requires a single permission for the endpoint (or every endpoint of a group).</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(Policy(permissionCode));

    /// <summary>
    /// Assigns a permission to every endpoint of a group from its HTTP method
    /// (GET/HEAD → view, POST → create, PUT/PATCH → edit, DELETE → delete).
    /// Endpoints matching one of <paramref name="adminRouteSuffixes"/> (see <see cref="MatchesAdminRoute"/>) get
    /// <paramref name="adminPermission"/> instead. Endpoints that already declare a permission or
    /// the PlatformAdmin policy, or are explicitly anonymous, are left untouched.
    /// </summary>
    /// <remarks>
    /// Runs as a <c>Finally</c> convention so it sees the endpoint's complete metadata
    /// (HTTP methods and any endpoint-level authorization) before deciding.
    /// </remarks>
    public static TBuilder RequirePermissionsByMethod<TBuilder>(
        this TBuilder builder,
        string view,
        string create,
        string edit,
        string delete,
        string? adminPermission = null,
        params string[] adminRouteSuffixes)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpoint =>
        {
            if (HasExplicitPolicy(endpoint.Metadata) || endpoint.Metadata.OfType<IAllowAnonymous>().Any())
            {
                return;
            }

            var pattern = (endpoint as RouteEndpointBuilder)?.RoutePattern.RawText?.TrimEnd('/') ?? string.Empty;
            string permission;

            var methods = endpoint.Metadata.OfType<IHttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? [];
            permission = adminPermission is not null && adminRouteSuffixes.Any(s => MatchesAdminRoute(s, pattern, methods))
                ? adminPermission
                : PermissionForMethods(methods, view, create, edit, delete);

            endpoint.Metadata.Add(new AuthorizeAttribute(Policy(permission)));
        });

        return builder;
    }

    internal static string PermissionForMethods(
        IReadOnlyList<string> methods, string view, string create, string edit, string delete)
    {
        // An endpoint without a specific method (MapMethods/Map) is treated as the most
        // privileged method it may serve.
        if (methods.Count == 0) return delete;
        if (methods.Any(m => HttpMethods.IsDelete(m))) return delete;
        if (methods.Any(m => HttpMethods.IsPut(m) || HttpMethods.IsPatch(m))) return edit;
        if (methods.Any(m => HttpMethods.IsPost(m))) return create;
        return view;
    }

    /// <summary>
    /// An admin route entry is a route suffix, optionally prefixed with an HTTP method:
    /// <c>"accounting/repair"</c> (any method) or <c>"POST investors"</c> (POST only).
    /// </summary>
    internal static bool MatchesAdminRoute(string entry, string pattern, IReadOnlyList<string> methods)
    {
        var parts = entry.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var (method, suffix) = parts.Length == 2 ? (parts[0], parts[1]) : ((string?)null, parts[0]);

        if (method is not null && !methods.Any(m => string.Equals(m, method, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return pattern.EndsWith("/" + suffix.Trim('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasExplicitPolicy(IList<object> metadata) =>
        metadata.OfType<IAuthorizeData>().Any(a =>
            a.Policy is not null &&
            (a.Policy.StartsWith(PermissionPolicyPrefix, StringComparison.OrdinalIgnoreCase) ||
             a.Policy == AuthorizationPolicies.PlatformAdmin));
}
