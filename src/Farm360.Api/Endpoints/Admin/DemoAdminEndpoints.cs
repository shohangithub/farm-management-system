using Farm360.Api.Authorization;
using Farm360.Application.Common.Interfaces;
using Farm360.Application.Demo;

namespace Farm360.Api.Endpoints.Admin;

/// <summary>Platform administration of the public demo farm.</summary>
public static class DemoAdminEndpoints
{
    public static IEndpointRouteBuilder MapDemoAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/admin/demo")
            .RequireAuthorization(AuthorizationPolicies.PlatformAdmin)
            .WithTags("Admin");

        // Builds a fresh demo tenant (dates relative to today) in the background; when it is ready
        // the demo login moves to it and the previous demo tenant is retired.
        group.MapPost("/rebuild", (IBackgroundJobService jobs) =>
        {
            var jobId = jobs.Enqueue<DemoTenantManager>(m => m.RebuildAsync(CancellationToken.None));
            return Results.Accepted(value: new
            {
                jobId,
                message = "The demo farm is being rebuilt in the background. It usually takes a minute or two.",
            });
        })
        .Produces(StatusCodes.Status202Accepted);

        // Permanently deletes RETIRED demo tenants only (demo slug, cancelled, older than the
        // published one, never the newest). ?dryRun=true reports what would go without deleting.
        group.MapPost("/purge-retired", async (bool? dryRun, DemoTenantManager demo, CancellationToken cancellationToken) =>
        {
            var summary = await demo.PurgeRetiredAsync(dryRun ?? false, cancellationToken);
            return summary is null
                ? Results.Conflict(new { message = "A demo build is running. Try again when it has finished." })
                : Results.Ok(summary);
        })
        .Produces<DemoPurgeSummary>()
        .Produces(StatusCodes.Status409Conflict);

        return app;
    }
}
