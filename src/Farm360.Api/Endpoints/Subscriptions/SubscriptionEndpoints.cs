using Farm360.Application.Subscriptions.Commands;
using Farm360.Application.Subscriptions.DTOs;
using Farm360.Application.Subscriptions.Queries;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        // 4. Subscribe or upgrade to Monthly, Yearly, or One-Time plan -- self-reported payment,
        // recorded as Pending until an admin verifies it (see /admin/subscriptions/pending below).
        group.MapPost("/subscribe", async ([FromBody] SubscribeTenantCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("SubscribeTenant")
        .WithSummary("Submit a payment reference for a Monthly, Yearly, or One-Time plan, pending verification")
        .Produces<TenantSubscriptionRecordDto>(StatusCodes.Status200OK)
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

        // 7. Admin: list self-reported payments awaiting verification, across every tenant.
        // NOTE: like /admin/extend-trial above, this only requires an authenticated user, not a
        // platform-admin role -- this codebase has no cross-tenant admin permission/policy yet.
        // Scoping this to a real platform-admin policy is a follow-up, not introduced here.
        group.MapGet("/admin/subscriptions/pending", async (ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new Farm360.Application.Subscriptions.Queries.GetPendingSubscriptionsQuery(), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("GetPendingSubscriptions")
        .WithSummary("List self-reported subscription payments awaiting verification, across all tenants")
        .Produces<IReadOnlyList<Farm360.Application.Subscriptions.Queries.PendingSubscriptionDto>>(StatusCodes.Status200OK);

        // 8. Admin: approve a pending payment -- activates the tenant's subscription.
        group.MapPost("/admin/subscriptions/{id:guid}/approve", async (
            Guid id,
            [FromBody] ApproveSubscriptionRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new ApprovePendingSubscriptionCommand(id, request?.VerifiedReference), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("ApprovePendingSubscription")
        .WithSummary("Verify a self-reported payment and activate the subscription it describes")
        .Produces<TenantSubscriptionStatusDto>(StatusCodes.Status200OK);

        // 10. Start a verified online checkout (SSLCommerz) -- activates automatically once the
        // gateway confirms payment, unlike /subscribe's self-reported-reference flow above.
        group.MapPost("/checkout/initiate", async (
            [FromBody] Farm360.Application.Subscriptions.Commands.InitiateSubscriptionCheckoutCommand command,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        })
        .RequireAuthorization()
        .WithName("InitiateSubscriptionCheckout")
        .WithSummary("Starts an SSLCommerz hosted checkout session for a subscription plan")
        .Produces<Farm360.Application.Subscriptions.Commands.CheckoutSessionDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // 11. SSLCommerz IPN (Instant Payment Notification) -- server-to-server, authoritative.
        // Anonymous: SSLCommerz calls this directly, with no Farm360 user session.
        group.MapPost("/checkout/ipn", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var tranId = form["tran_id"].ToString();
            var valId = form["val_id"].ToString();

            if (string.IsNullOrWhiteSpace(tranId) || string.IsNullOrWhiteSpace(valId))
            {
                return Results.BadRequest();
            }

            await sender.Send(new Farm360.Application.Subscriptions.Commands.FinalizeSubscriptionCheckoutCommand(tranId, valId), ct);
            return Results.Ok();
        })
        .AllowAnonymous()
        .WithName("SslCommerzIpn")
        .WithSummary("SSLCommerz server-to-server payment notification");

        // 12/13/14. Browser landing after checkout -- best-effort finalize (IPN is authoritative),
        // then redirect the user back into the app with a result the billing page can show.
        group.MapPost("/checkout/success", async (HttpContext http, ISender sender, CancellationToken ct) =>
        {
            var form = await http.Request.ReadFormAsync(ct);
            var tranId = form["tran_id"].ToString();
            var valId = form["val_id"].ToString();
            var frontendUrl = http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Farm360.Infrastructure.Payments.SslCommerzOptions>>().Value.FrontendBaseUrl;

            if (!string.IsNullOrWhiteSpace(tranId) && !string.IsNullOrWhiteSpace(valId))
            {
                await sender.Send(new Farm360.Application.Subscriptions.Commands.FinalizeSubscriptionCheckoutCommand(tranId, valId), ct);
            }

            return Results.Redirect($"{frontendUrl}/settings/billing?checkout=success");
        })
        .AllowAnonymous()
        .WithName("SslCommerzSuccessRedirect")
        .WithSummary("Browser landing page after a successful SSLCommerz checkout");

        group.MapPost("/checkout/fail", (HttpContext http) =>
        {
            var frontendUrl = http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Farm360.Infrastructure.Payments.SslCommerzOptions>>().Value.FrontendBaseUrl;
            return Results.Redirect($"{frontendUrl}/settings/billing?checkout=fail");
        })
        .AllowAnonymous()
        .WithName("SslCommerzFailRedirect")
        .WithSummary("Browser landing page after a failed SSLCommerz checkout");

        group.MapPost("/checkout/cancel", (HttpContext http) =>
        {
            var frontendUrl = http.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<Farm360.Infrastructure.Payments.SslCommerzOptions>>().Value.FrontendBaseUrl;
            return Results.Redirect($"{frontendUrl}/settings/billing?checkout=cancel");
        })
        .AllowAnonymous()
        .WithName("SslCommerzCancelRedirect")
        .WithSummary("Browser landing page after a cancelled SSLCommerz checkout");

        // 9. Admin: reject a pending payment -- the claimed reference did not check out.
        group.MapPost("/admin/subscriptions/{id:guid}/reject", async (
            Guid id,
            [FromBody] RejectSubscriptionRequest? request,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.Send(
                new RejectPendingSubscriptionCommand(id, request?.Reason), ct);
            return Results.Ok(result);
        })
        .RequireAuthorization()
        .WithName("RejectPendingSubscription")
        .WithSummary("Reject a self-reported payment that could not be verified")
        .Produces<TenantSubscriptionRecordDto>(StatusCodes.Status200OK);

        return group;
    }
}

public sealed record ApproveSubscriptionRequest(string? VerifiedReference);
public sealed record RejectSubscriptionRequest(string? Reason);
