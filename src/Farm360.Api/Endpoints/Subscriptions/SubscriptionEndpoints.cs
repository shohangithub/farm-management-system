using Farm360.Application.Subscriptions.Commands;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Application.Subscriptions.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;

namespace Farm360.Api.Endpoints.Subscriptions;

public static class SubscriptionEndpoints
{
    public static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Subscriptions");

        // 1. Get current tenant's subscription status & quotas
        group.MapGet("/current", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetCurrentTenantSubscriptionQuery(), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetCurrentTenantSubscription")
        .WithSummary("Retrieve the current tenant's subscription tier, billing cycle, trial status, and resource usage")
        .Produces<TenantSubscriptionStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 2. Get available plans (Monthly, Yearly, OneTime) & trial options (3, 7, 10 days)
        group.MapGet("/plans", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSubscriptionPlansQuery(), ct);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetSubscriptionPlans")
        .WithSummary("Get the available SaaS subscription plans and trial duration options")
        .Produces<SubscriptionCatalogDto>(StatusCodes.Status200OK);

        // 3. Start a free trial (3, 7, or 10 days)
        group.MapPost("/start-trial", async ([FromBody] StartTenantTrialCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("StartTenantTrial")
        .WithSummary("Activate a free trial period (3, 7, or 10 days) for the current tenant")
        .Produces<TenantSubscriptionStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // 4. Subscribe or upgrade to Monthly, Yearly, or One-Time plan
        group.MapPost("/subscribe", async ([FromBody] SubscribeTenantCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("SubscribeTenant")
        .WithSummary("Subscribe to or renew a Monthly, Yearly, or One-Time lifetime plan")
        .Produces<TenantSubscriptionStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // 5. Subscription & payment invoice history
        group.MapGet("/history", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetSubscriptionHistoryQuery(), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetSubscriptionHistory")
        .WithSummary("Get list of past subscription transactions and invoices for the current tenant")
        .Produces<IReadOnlyList<TenantSubscriptionRecordDto>>(StatusCodes.Status200OK);

        // 6. Admin extend trial
        group.MapPost("/admin/extend-trial", async ([FromBody] AdminExtendTrialCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("AdminExtendTrial")
        .WithSummary("Admin endpoint to extend a tenant's trial period by additional days")
        .Produces<TenantSubscriptionStatusDto>(StatusCodes.Status200OK);

        return group;
    }
}
